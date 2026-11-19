using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Пункт графика, о котором напоминаем покупателю.
public record PaymentNotice(Guid DealId, DateOnly DueDate, decimal Left, Currency Currency, int Apartment,
    Guid? CustomerUserId, Guid OrganizationId, int OverdueDays);

// Сообщения покупателю о его сделке. Напоминания о платежах — в кабинет, Telegram и на почту; остальные события
// (бронь, этап сделки, платёж принят, документ, сообщение менеджера) — в Telegram. Каналы и события выбирает сам
// покупатель (AppUser.Notify*); язык — его (ru, tg, en). Отправка — очередь Hangfire с повторами.
// Все методы событий вызываются после сохранения данных и ничего не бросают: сбой уведомления не отменяет бронь или платёж.
public class BuyerNotifier(
    AppDbContext db,
    IBuyerMessageQueue queue,
    IOptions<EmailOptions> site,
    UnsubscribeTokens unsubscribe,
    IPresenceTracker presence,
    TimeProvider timeProvider,
    ILogger<BuyerNotifier> logger)
{
    // «Покупатель не в сети»: в кабинете не был дольше этого — о сообщении менеджера напомним в Telegram.
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(2);

    private string SiteOrigin => Uri.TryCreate(site.Value.InviteUrl, UriKind.Absolute, out var url) ? url.GetLeftPart(UriPartial.Authority) : "";
    private string? CabinetUrl => SiteOrigin.Length > 0 ? SiteOrigin + "/cabinet" : null;
    private string ApiOrigin => !string.IsNullOrWhiteSpace(site.Value.ApiUrl) ? site.Value.ApiUrl.TrimEnd('/') : SiteOrigin;
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private record Recipient(Guid Id, string Lang, long? Chat, bool ByTelegram, bool ByEmail, NotifyEventGroups Events,
        string? Email, bool EmailConfirmed, string Name, bool Active);

    private async Task<Recipient?> RecipientAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is null)
            return null;
        var r = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new Recipient(u.Id, u.NotifyLanguage ?? "ru", u.TelegramChatId, u.NotifyByTelegram, u.NotifyByEmail, u.NotifyEvents,
                u.Email, u.EmailConfirmed, u.FullName, u.IsActive))
            .FirstOrDefaultAsync(ct);
        return r is { Active: true } ? r with { Lang = BuyerTexts.Lang(r.Lang) } : null;
    }

    // ---------- напоминания о платежах: кабинет + Telegram + почта ----------

    // Сколько новых сообщений поставлено (по всем каналам). Повтор того же события по тому же пункту и каналу
    // не создаётся (DedupeKey); ручное «Напомнить сейчас» (createdById) — без ключа, его лимит проверяет вызывающий.
    public async Task<int> PaymentReminderAsync(PaymentNotice n, string evt, Guid? createdById, CancellationToken ct)
    {
        var r = await RecipientAsync(n.CustomerUserId, ct);
        if (r is null)
            return 0;
        var lang = r.Lang;
        var title = BuyerTexts.ReminderTitle(lang, evt);
        var text = BuyerTexts.ReminderText(lang, evt, n.Left, n.Currency, n.Apartment, n.DueDate, n.OverdueDays);
        string? Key(string channel) => createdById is null ? $"{evt}:{n.DealId:N}:{n.DueDate:yyyyMMdd}:{channel}:{r.Id:N}" : null;
        BuyerMessage Message(string channel, string status) => new()
        {
            UserId = r.Id, OrganizationId = n.OrganizationId, DealId = n.DealId, DueDate = n.DueDate, Event = evt, Channel = channel,
            Status = status, Subject = title, Text = text, CreatedById = createdById, DedupeKey = Key(channel),
            SentAt = status == BuyerMessageStatus.Sent ? Now : null
        };

        var count = 0;
        // Колокольчик кабинета — всегда.
        var inApp = Message(BuyerChannels.InApp, BuyerMessageStatus.Sent);
        var bell = new Notification
        {
            UserId = r.Id, Channel = Notification.InAppChannel, Kind = evt, DealId = n.DealId,
            // У ручного напоминания срока нет: иначе второе нажатие через сутки упёрлось бы в уникальный индекс колокольчика.
            DueDate = createdById is null ? n.DueDate : null, Title = title, Text = text, SentAt = Now
        };
        if (await TryAddAsync(inApp, bell, ct))
            count++;

        var wantsPayments = r.Events.HasFlag(NotifyEventGroups.Payments);
        if (wantsPayments && r.Chat is not null && r.ByTelegram)
        {
            var m = Message(BuyerChannels.Telegram, BuyerMessageStatus.Pending);
            m.Text = BuyerTexts.TelegramReminder(lang, evt, text, CabinetUrl);
            if (await TryAddAsync(m, null, ct))
            {
                queue.Enqueue(m.Id);
                count++;
            }
        }

        if (wantsPayments && r.ByEmail && r.EmailConfirmed && !string.IsNullOrWhiteSpace(r.Email))
        {
            var org = await db.Organizations.IgnoreQueryFilters().AsNoTracking().Where(o => o.Id == n.OrganizationId)
                .Select(o => new { o.Name, o.Phone, o.Email, o.Telegram, o.Address }).FirstOrDefaultAsync(ct);
            var brand = string.IsNullOrWhiteSpace(org?.Name) ? "Застройщик" : org.Name.Trim();
            // Подпись письма — контакты организации, только заполненные (без выдуманных по умолчанию).
            var contacts = new[] { org?.Phone, org?.Email, org?.Telegram, org?.Address }
                .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()).ToList();
            var unsubscribeUrl = $"{ApiOrigin}/api/v1/public/unsubscribe?t={unsubscribe.Create(r.Id)}";
            var letter = EmailTemplates.PaymentReminder(r.Email, lang, brand, r.Name, title, text, CabinetUrl, unsubscribeUrl, contacts);
            var m = Message(BuyerChannels.Email, BuyerMessageStatus.Pending);
            (m.Subject, m.Text, m.Html) = (letter.Subject, letter.Text, letter.Html);
            if (await TryAddAsync(m, null, ct))
            {
                queue.Enqueue(m.Id);
                count++;
            }
        }
        return count;
    }


    // ---------- события сделки: только Telegram ----------

    public Task BookingCreatedAsync(Guid bookingId, CancellationToken ct = default) => SafeAsync("о новой брони", async () =>
    {
        var b = await BookingAsync(bookingId, ct);
        if (b is not null)
            await TelegramAsync(b.UserId, b.OrganizationId, null, BuyerEvents.BookingCreated,
                lang => BuyerTexts.BookingCreated(lang, b.Number, DushanbeTime.ToLocal(b.ExpiresAt), CabinetUrl), null, ct);
    });

    // За 6 часов до конца брони; ключ с моментом окончания: продлённая бронь напомнит ещё раз — перед новым сроком.
    public Task BookingExpiringAsync(Guid bookingId, CancellationToken ct = default) => SafeAsync("о конце брони", async () =>
    {
        var b = await BookingAsync(bookingId, ct);
        if (b is not null)
            await TelegramAsync(b.UserId, b.OrganizationId, null, BuyerEvents.BookingExpiring,
                lang => BuyerTexts.BookingExpiring(lang, b.Number, DushanbeTime.ToLocal(b.ExpiresAt), CabinetUrl),
                $"{BuyerEvents.BookingExpiring}:{bookingId:N}:{b.ExpiresAt:yyyyMMddHHmm}", ct);
    });

    public Task BookingReleasedAsync(Guid bookingId, CancellationToken ct = default) => SafeAsync("о снятой брони", async () =>
    {
        var b = await BookingAsync(bookingId, ct);
        if (b is not null)
            await TelegramAsync(b.UserId, b.OrganizationId, null, BuyerEvents.BookingReleased,
                lang => BuyerTexts.BookingReleased(lang, b.Number, CabinetUrl), $"{BuyerEvents.BookingReleased}:{bookingId:N}", ct);
    });

    public Task DealStageChangedAsync(Guid dealId, CancellationToken ct = default) => SafeAsync("об этапе сделки", async () =>
    {
        var d = await DealAsync(dealId, ct);
        if (d is not null)
            await TelegramAsync(d.UserId, d.OrganizationId, dealId, BuyerEvents.DealStage,
                lang => BuyerTexts.DealStageChanged(lang, d.Number, d.Stage, CabinetUrl), null, ct);
    });

    public Task PaymentReceivedAsync(Guid paymentId, CancellationToken ct = default) => SafeAsync("о принятом платеже", async () =>
    {
        var p = await db.Payments.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == paymentId)
            .Select(x => new { x.DealId, x.Amount, x.Currency, x.PaidAt, x.Deal.Customer.UserId, x.Deal.Apartment.Number,
                x.Deal.Apartment.Floor.Building.OrganizationId })
            .FirstOrDefaultAsync(ct);
        if (p is not null)
            await TelegramAsync(p.UserId, p.OrganizationId, p.DealId, BuyerEvents.PaymentReceived,
                lang => BuyerTexts.PaymentReceived(lang, p.Number, p.Amount, p.Currency, p.PaidAt, CabinetUrl), $"{BuyerEvents.PaymentReceived}:{paymentId:N}", ct);
    });

    // Ссылка — на кабинет, не на файл: файл открывается только после входа.
    public Task DocumentAddedAsync(Guid dealId, CancellationToken ct = default) => SafeAsync("о новом документе", async () =>
    {
        var d = await DealAsync(dealId, ct);
        if (d is not null)
            await TelegramAsync(d.UserId, d.OrganizationId, dealId, BuyerEvents.DocumentAdded,
                lang => BuyerTexts.DocumentAdded(lang, d.Number, CabinetUrl), null, ct);
    });

    // Сообщение менеджера, когда покупателя нет в кабинете; текст чата в Telegram не уходит — только «вам написали».
    public Task ChatMessageAsync(Guid dealId, CancellationToken ct = default) => SafeAsync("о сообщении менеджера", async () =>
    {
        var d = await DealAsync(dealId, ct);
        if (d?.UserId is not { } userId)
            return;
        var lastSeen = await presence.LastSeenAsync(userId, ct);
        if (lastSeen is { } seen && Now - seen < OfflineAfter)
            return;
        await TelegramAsync(userId, d.OrganizationId, dealId, BuyerEvents.ChatMessage,
            lang => BuyerTexts.ChatMessage(lang, d.Number, CabinetUrl), null, ct);
    });

    private record BookingInfo(Guid? UserId, int Number, DateTime ExpiresAt, Guid OrganizationId);

    private Task<BookingInfo?> BookingAsync(Guid bookingId, CancellationToken ct) =>
        db.Bookings.IgnoreQueryFilters().AsNoTracking().Where(b => b.Id == bookingId && b.CustomerId != null)
            .Select(b => new BookingInfo(b.Customer!.UserId, b.Apartment.Number, b.ExpiresAt, b.Apartment.Floor.Building.OrganizationId))
            .FirstOrDefaultAsync(ct);

    private record DealInfo(Guid? UserId, int Number, DealStage Stage, Guid OrganizationId);

    private Task<DealInfo?> DealAsync(Guid dealId, CancellationToken ct) =>
        db.Deals.IgnoreQueryFilters().AsNoTracking().Where(d => d.Id == dealId)
            .Select(d => new DealInfo(d.Customer.UserId, d.Apartment.Number, d.Stage, d.Apartment.Floor.Building.OrganizationId))
            .FirstOrDefaultAsync(ct);

    private async Task TelegramAsync(Guid? userId, Guid organizationId, Guid? dealId, string evt, Func<string, string> text, string? dedupeKey, CancellationToken ct)
    {
        var r = await RecipientAsync(userId, ct);
        if (r?.Chat is null || !r.ByTelegram || !r.Events.HasFlag(BuyerEvents.GroupOf(evt)))
            return;
        var m = new BuyerMessage
        {
            UserId = r.Id, OrganizationId = organizationId, DealId = dealId, Event = evt, Channel = BuyerChannels.Telegram,
            Status = BuyerMessageStatus.Pending, Text = text(r.Lang), DedupeKey = dedupeKey is null ? null : $"{dedupeKey}:{r.Id:N}"
        };
        if (await TryAddAsync(m, null, ct))
            queue.Enqueue(m.Id);
    }

    // Запись с ключом, который уже есть, не добавляется (гонка двух запусков упирается в уникальный индекс).
    private async Task<bool> TryAddAsync(BuyerMessage message, Notification? bell, CancellationToken ct)
    {
        if (message.DedupeKey is { } key && await db.BuyerMessages.IgnoreQueryFilters().AnyAsync(m => m.DedupeKey == key, ct))
            return false;
        db.BuyerMessages.Add(message);
        if (bell is not null)
            db.Notifications.Add(bell);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException) when (message.DedupeKey is not null)
        {
            db.Entry(message).State = EntityState.Detached;
            if (bell is not null)
                db.Entry(bell).State = EntityState.Detached;
            return false;
        }
    }

    private async Task SafeAsync(string what, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Не удалось поставить сообщение покупателю {What}", what);
        }
    }
}

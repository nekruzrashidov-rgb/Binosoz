using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Тексты сообщений в Telegram (Markdown). Имена и описания экранируются: «_» или «*» в ФИО не ломают разметку.
public static class TelegramMessages
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static string Money(decimal amount, Currency currency) => $"{amount.ToString("#,0.00", Russian)} {currency}";

    public static string Payment(string customer, int apartment, decimal amount, Currency currency, DateOnly paidAt) =>
        $"💰 *Новый платёж*\n{Escape(customer)}, кв. {apartment}, {Money(amount, currency)}, {paidAt:dd.MM.yyyy}";

    public static string Overdue(string customer, int apartment, decimal amount, Currency currency, int days) =>
        $"🔴 *Просрочка*\n{Escape(customer)}, кв. {apartment}, {Money(amount, currency)}, просрочено {days} дн.";

    public static string OnlinePayment(int apartment, decimal amount, Currency currency) =>
        $"✅ *Оплата получена*\nКв. {apartment}: {Money(amount, currency)} картой. Спасибо!";

    public static string Reminder(decimal amount, Currency currency, DateOnly due) =>
        $"🟡 *Напоминание*\nПлатёж {Money(amount, currency)} через 3 дня, {due:dd.MM.yyyy}";

    public static string Booking(int apartment, string client, decimal price, Currency currency) =>
        $"🏠 *Новая бронь*\nКв. {apartment}, {Escape(client)}, {Money(price, currency)}";

    public static string Lead(string name, string? building, int? apartment) =>
        $"📝 *Новая заявка*\n{Escape(name)}" + (building is null ? "" : $", {Escape(building)}") + (apartment is null ? "" : $", кв. {apartment}");

    public static string WeeklyReport(string building, DateOnly start, DateOnly end, string? link) =>
        $"📊 *Еженедельный отчёт готов*\n{Escape(building)}, {start:dd.MM.yyyy} — {end:dd.MM.yyyy}" + (link is null ? "" : $"\n{link}");

    // Спецсимволы Markdown (старого режима Telegram) — с обратной косой чертой.
    public static string Escape(string text) =>
        string.Concat(text.Select(c => c is '_' or '*' or '`' or '[' ? "\\" + c : c.ToString()));
}

// Кому и что отправить в Telegram: получатели — по роли, организации и доступным домам, только с привязанным чатом.
// Все методы вызываются после сохранения данных и ничего не бросают: сбой Telegram не отменяет платёж или бронь.
public class TelegramAlertService(AppDbContext db, ITelegramNotifier telegram, IOptions<EmailOptions> site, ILogger<TelegramAlertService> logger)
{
    // Отчёты лежат в CRM; адрес сайта — тот же, что в письмах-приглашениях.
    private string? CrmUrl => Uri.TryCreate(site.Value.InviteUrl, UriKind.Absolute, out var url) ? url.GetLeftPart(UriPartial.Authority) + "/crm" : null;

    public Task PaymentReceivedAsync(Guid paymentId, CancellationToken ct = default) => SafeAsync("о платеже", async () =>
    {
        var p = await db.Payments.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == paymentId)
            .Select(x => new { x.Amount, x.Currency, x.PaidAt, x.Deal.Customer.FullName, x.Deal.Apartment.Number,
                x.Deal.Apartment.Floor.BuildingId, x.Deal.Apartment.Floor.Building.OrganizationId })
            .SingleAsync(ct);
        await SendToStaffAsync(p.OrganizationId, p.BuildingId, [RoleNames.Owner, RoleNames.Accountant],
            TelegramMessages.Payment(p.FullName, p.Number, p.Amount, p.Currency, p.PaidAt), ct);
    });

    public Task BookingCreatedAsync(Guid bookingId, CancellationToken ct = default) => SafeAsync("о брони", async () =>
    {
        var b = await db.Bookings.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == bookingId)
            .Select(x => new { x.Apartment.Number, x.Apartment.Price, x.Apartment.Currency,
                Client = x.Customer != null ? x.Customer.FullName : x.Lead != null ? x.Lead.Name : "",
                x.Apartment.Floor.BuildingId, x.Apartment.Floor.Building.OrganizationId })
            .SingleAsync(ct);
        await SendToStaffAsync(b.OrganizationId, b.BuildingId, [RoleNames.Owner, RoleNames.Manager],
            TelegramMessages.Booking(b.Number, b.Client, b.Price, b.Currency), ct);
    });

    public Task LeadCreatedAsync(Guid leadId, CancellationToken ct = default) => SafeAsync("о заявке", async () =>
    {
        var l = await db.Leads.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == leadId)
            .Select(x => new { x.Name, x.OrganizationId, x.BuildingId,
                Building = db.Buildings.IgnoreQueryFilters().Where(b => b.Id == x.BuildingId).Select(b => b.NameRu).FirstOrDefault(),
                Apartment = x.Apartment != null ? (int?)x.Apartment.Number : null })
            .SingleAsync(ct);
        await SendToStaffAsync(l.OrganizationId, l.BuildingId, [RoleNames.Owner, RoleNames.Manager],
            TelegramMessages.Lead(l.Name, l.Building, l.Apartment), ct);
    });

    public Task WeeklyReportReadyAsync(Guid reportId, CancellationToken ct = default) => SafeAsync("об отчёте", async () =>
    {
        var r = await db.WeeklyReports.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == reportId)
            .Select(x => new { x.PeriodStart, x.PeriodEnd, x.BuildingId, x.Building.NameRu, x.Building.OrganizationId })
            .SingleAsync(ct);
        await SendToStaffAsync(r.OrganizationId, r.BuildingId, [RoleNames.Owner],
            TelegramMessages.WeeklyReport(r.NameRu, r.PeriodStart, r.PeriodEnd, CrmUrl), ct);
    });

    // Разные тексты разным пользователям (просрочки, напоминания): одно сообщение на пару «пользователь — текст».
    public Task SendToUsersAsync(IReadOnlyCollection<(Guid UserId, string Text)> messages, CancellationToken ct = default) => SafeAsync("о платежах по графику", async () =>
    {
        if (messages.Count == 0)
            return;
        var ids = messages.Select(m => m.UserId).Distinct().ToList();
        var chats = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.IsActive && u.TelegramChatId != null)
            .ToDictionaryAsync(u => u.Id, u => u.TelegramChatId!.Value, ct);
        foreach (var (userId, text) in messages)
            if (chats.TryGetValue(userId, out var chat))
                await telegram.SendAsync(chat, text, ct);
    });

    // Сотрудники организации с одной из ролей, которым доступен дом: без закреплённых домов — все дома организации.
    private async Task SendToStaffAsync(Guid organizationId, Guid? buildingId, string[] roles, string text, CancellationToken ct)
    {
        var chats = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.OrganizationId == organizationId && u.TelegramChatId != null
                        && db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && roles.Contains(r.Name!)))
                        && (buildingId == null || !db.UserBuildings.Any(ub => ub.UserId == u.Id)
                            || db.UserBuildings.Any(ub => ub.UserId == u.Id && ub.BuildingId == buildingId)))
            .Select(u => u.TelegramChatId!.Value)
            .Distinct()
            .ToListAsync(ct);
        foreach (var chat in chats)
            await telegram.SendAsync(chat, text, ct);
    }

    private async Task SafeAsync(string what, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Не удалось отправить в Telegram уведомление {What}", what);
        }
    }
}

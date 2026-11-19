using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

// Сообщения покупателю: напоминания о платежах (кабинет + Telegram + почта), привязка Telegram через бота,
// события бота, отписка из письма, журнал и «Напомнить сейчас» в CRM.
public partial class BuyerNotificationTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static long _chat = 7_100_000_000;
    private static long NewChat() => Interlocked.Increment(ref _chat);

    private record Buyer(DealScenario Deal, HttpClient Client, Guid UserId, string Email);

    // Сделка с графиком, покупатель вошёл в кабинет (почта подтверждена); первый неоплаченный пункт — на due.
    private async Task<Buyer> BuyerWithItemDueAsync(DateOnly due)
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: TestScenarios.Today.AddMonths(1));
        var client = await factory.CreateCustomerClientAsync(deal.Phone);
        var (userId, email) = await factory.WithDbAsync(async db =>
        {
            var item = await db.PaymentScheduleItems.Where(i => i.Plan.DealId == deal.DealId).OrderBy(i => i.DueDate).FirstAsync();
            item.DueDate = due;
            await db.SaveChangesAsync();
            var c = await db.Customers.Where(x => x.Id == deal.CustomerId).Select(x => new { x.UserId, x.Email }).SingleAsync();
            return (c.UserId!.Value, c.Email!);
        });
        return new Buyer(deal, client, userId, email);
    }

    private Task LinkChatAsync(Guid userId, long? chat) => factory.WithDbAsync(async db =>
    {
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.TelegramChatId, chat));
    });

    private Task<List<BuyerMessage>> MessagesAsync(Guid dealId) =>
        factory.WithDbAsync(db => db.BuyerMessages.IgnoreQueryFilters().Where(m => m.DealId == dealId).OrderBy(m => m.CreatedAt).ToListAsync());

    private Task<int> RunAsync(Func<PaymentAlertService, Task<int>> run) =>
        factory.WithDbAsync(async _ =>
        {
            var result = 0;
            await factory.WithServiceAsync<PaymentAlertService>(async s => result = await run(s));
            return result;
        });

    private Task<HttpResponseMessage> WebhookAsync(string text, long chat, string? secret = SitoraApiFactory.TelegramWebhookSecret,
        string? header = SitoraApiFactory.TelegramWebhookSecret, string lang = "ru")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/telegram/webhook/{secret}")
        {
            Content = JsonContent.Create(new
            {
                update_id = 1,
                message = new { message_id = 1, text, chat = new { id = chat, type = "private" }, from = new { id = chat, language_code = lang } }
            })
        };
        if (header is not null)
            request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", header);
        return factory.CreateClient().SendAsync(request);
    }

    // ---------- напоминания о платежах ----------

    [Fact]
    public async Task DueToday_GoesToCabinetTelegramAndEmail_OncePerChannel()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today);
        var chat = NewChat();
        await LinkChatAsync(b.UserId, chat);

        await RunAsync(s => s.RemindDueTodayAsync());
        await RunAsync(s => s.RemindDueTodayAsync());

        var mine = (await MessagesAsync(b.Deal.DealId)).Where(m => m.Event == BuyerEvents.PaymentDueToday).ToList();
        Assert.Equal(new[] { BuyerChannels.Email, BuyerChannels.InApp, BuyerChannels.Telegram }, mine.Select(m => m.Channel).Order(StringComparer.Ordinal));
        Assert.All(mine, m => Assert.Equal(BuyerMessageStatus.Sent, m.Status));

        var telegram = Assert.Single(factory.Telegram.To(chat));
        Assert.StartsWith("🔔 *Сегодня срок платежа*\nСегодня срок платежа: ", telegram);
        Assert.Contains("TJS по кв. №", telegram);
        Assert.Contains("[Открыть кабинет](", telegram);

        var letter = factory.Email.LastMessage(b.Email)!;
        var brand = await factory.WithDbAsync(db => db.Deals.IgnoreQueryFilters().Where(d => d.Id == b.Deal.DealId)
            .Select(d => d.Apartment.Floor.Building.Organization.Name).SingleAsync());
        Assert.Equal($"{brand}: Сегодня срок платежа", letter.Subject);
        Assert.Equal(brand, letter.FromName);
        Assert.Contains("Открыть кабинет", letter.Html);
        Assert.Contains("/api/v1/public/unsubscribe?t=", letter.Text);
        Assert.DoesNotContain("Binosoz", letter.Text);

        // В колокольчике — одно уведомление «Сегодня срок платежа».
        var bell = (await b.Client.GetFromJsonAsync<NotificationListResponse>("/api/v1/notifications", TestJson.Options))!;
        Assert.Single(bell.Items, n => n.Kind == BuyerEvents.PaymentDueToday && n.DealId == b.Deal.DealId);
    }

    [Fact]
    public async Task Preferences_EmailOff_OnlyCabinetAndTelegram_PaymentsOff_OnlyCabinet()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today.AddDays(PaymentAlertService.ReminderDaysBefore));
        var chat = NewChat();
        await LinkChatAsync(b.UserId, chat);

        var settings = (await b.Client.GetFromJsonAsync<NotificationSettingsResponse>("/api/v1/profile/notifications", TestJson.Options))!;
        Assert.True(settings is { Telegram: true, Email: true, TelegramLinked: true, TelegramAvailable: true, EmailAvailable: true });
        Assert.True(settings.Events.Payments);

        b.Client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tg");
        var saved = await b.Client.PutAsJsonAsync("/api/v1/profile/notifications",
            new UpdateNotificationSettingsRequest(true, false, new NotificationEventsDto(true, true, true, true, true)));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("tg", (await saved.Content.ReadFromJsonAsync<NotificationSettingsResponse>(TestJson.Options))!.Language);

        await RunAsync(s => s.RemindUpcomingAsync());
        var channels = (await MessagesAsync(b.Deal.DealId)).Where(m => m.Event == BuyerEvents.PaymentReminder).Select(m => m.Channel).ToList();
        Assert.Equal(2, channels.Count);
        Assert.DoesNotContain(BuyerChannels.Email, channels);
        // Таджикский: язык сохранён вместе с настройками.
        Assert.StartsWith("🟡 *Ёдраскунӣ дар бораи пардохт*\nПас аз 3 рӯз", Assert.Single(factory.Telegram.To(chat)));

        // Платежи выключены — только кабинет.
        var other = await BuyerWithItemDueAsync(TestScenarios.Today);
        var otherChat = NewChat();
        await LinkChatAsync(other.UserId, otherChat);
        await other.Client.PutAsJsonAsync("/api/v1/profile/notifications",
            new UpdateNotificationSettingsRequest(true, true, new NotificationEventsDto(true, true, false, true, true)));
        await RunAsync(s => s.RemindDueTodayAsync());
        Assert.Equal([BuyerChannels.InApp], (await MessagesAsync(other.Deal.DealId)).Where(m => m.Event == BuyerEvents.PaymentDueToday).Select(m => m.Channel));
        Assert.Empty(factory.Telegram.To(otherChat));
    }

    [Fact]
    public async Task Overdue_FirstDay_ThenSixDaysLater_NotDaily()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today.AddDays(-8));

        await RunAsync(s => s.NotifyOverdueAsync());
        await RunAsync(s => s.NotifyOverdueAsync());
        var first = (await MessagesAsync(b.Deal.DealId)).Where(m => m.Event == BuyerEvents.PaymentOverdue).ToList();
        Assert.Equal(2, first.Count); // кабинет и почта
        Assert.DoesNotContain(await MessagesAsync(b.Deal.DealId), m => m.Event == BuyerEvents.PaymentOverdueWeek);
        Assert.Contains("просрочен на 8 дн.", first[0].Text);

        // Прошло 6 дней с первого напоминания — второе, тоже один раз.
        await factory.WithDbAsync(async db =>
        {
            var ago = DateTime.UtcNow.AddDays(-PaymentAlertService.SecondOverdueAfterDays);
            await db.BuyerMessages.IgnoreQueryFilters().Where(m => m.DealId == b.Deal.DealId).ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAt, ago));
            await db.Notifications.Where(n => n.DealId == b.Deal.DealId).ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedAt, ago));
        });
        await RunAsync(s => s.NotifyOverdueAsync());
        await RunAsync(s => s.NotifyOverdueAsync());
        Assert.Equal(2, (await MessagesAsync(b.Deal.DealId)).Count(m => m.Event == BuyerEvents.PaymentOverdueWeek));
    }

    [Fact]
    public async Task Unsubscribe_LinkFromLetter_TurnsEmailOff_BadLinkIs404()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today);
        await RunAsync(s => s.RemindDueTodayAsync());
        var link = UnsubscribeLink().Match(factory.Email.LastMessage(b.Email)!.Text).Groups[1].Value;
        var client = factory.CreateClient();

        var page = await client.GetAsync($"/api/v1/public/unsubscribe?t={link}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("<form method=\"post\"", await page.Content.ReadAsStringAsync());
        // Открыть ссылку (как делают почтовые сканеры) — ничего не выключает.
        Assert.True(await factory.WithDbAsync(db => db.Users.Where(u => u.Id == b.UserId).Select(u => u.NotifyByEmail).SingleAsync()));

        var done = await client.PostAsync($"/api/v1/public/unsubscribe?t={link}", null);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.False(await factory.WithDbAsync(db => db.Users.Where(u => u.Id == b.UserId).Select(u => u.NotifyByEmail).SingleAsync()));

        var forged = link[..^4] + (link.EndsWith("AAAA") ? "BBBB" : "AAAA");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/v1/public/unsubscribe?t={forged}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/public/unsubscribe?t=x")).StatusCode);
    }

    [GeneratedRegex(@"unsubscribe\?t=([A-Za-z0-9_.-]+)")]
    private static partial Regex UnsubscribeLink();

    // ---------- привязка Telegram ----------

    [Fact]
    public async Task Link_StartWithToken_SavesChat_OneTime_StopUnlinks()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today.AddDays(20));
        var response = await b.Client.PostAsync("/api/v1/profile/telegram/link", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = (await response.Content.ReadFromJsonAsync<TelegramLinkResponse>(TestJson.Options))!;
        Assert.StartsWith($"https://t.me/{SitoraApiFactory.TelegramBotUsername}?start=", link.Url);
        Assert.InRange(link.ExpiresAt, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        var token = link.Url.Split("start=")[1];
        var chat = NewChat();

        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync($"/start {token}", chat)).StatusCode);
        Assert.Equal(chat, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == b.UserId).Select(u => u.TelegramChatId).SingleAsync()));
        Assert.StartsWith("✅ Готово!", factory.Telegram.To(chat).Last());

        // Второй раз та же ссылка не работает (другой чат не привяжется).
        var stranger = NewChat();
        await WebhookAsync($"/start {token}", stranger, lang: "tg");
        Assert.StartsWith("Пайванд кӯҳна шудааст", factory.Telegram.To(stranger).Last());
        Assert.Equal(chat, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == b.UserId).Select(u => u.TelegramChatId).SingleAsync()));

        // /stop в боте — чат отвязан.
        await WebhookAsync("/stop", chat);
        Assert.Null(await factory.WithDbAsync(db => db.Users.Where(u => u.Id == b.UserId).Select(u => u.TelegramChatId).SingleAsync()));
        Assert.StartsWith("Уведомления отключены", factory.Telegram.To(chat).Last());
    }

    [Fact]
    public async Task Link_ForgedOrExpiredToken_NotLinked()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today.AddDays(20));
        var chat = NewChat();

        await WebhookAsync("/start forged-token-0123456789abcdef", chat);
        Assert.StartsWith("Ссылка устарела", factory.Telegram.To(chat).Last());

        // Просроченный билет в кэше (запись ещё не удалена, а срок уже прошёл).
        const string token = "expired-token-0123456789abcdefgh";
        var key = "tg-link:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await factory.WithServiceAsync<IDistributedCache>(cache => cache.SetAsync(key,
            JsonSerializer.SerializeToUtf8Bytes(new { UserId = b.UserId, Language = "ru", ExpiresAt = DateTime.UtcNow.AddMinutes(-1) })));
        await WebhookAsync($"/start {token}", chat);
        Assert.Null(await factory.WithDbAsync(db => db.Users.Where(u => u.Id == b.UserId).Select(u => u.TelegramChatId).SingleAsync()));
        Assert.Equal(2, factory.Telegram.To(chat).Count(m => m.StartsWith("Ссылка устарела")));
    }

    [Fact]
    public async Task Webhook_WithoutSecret_Is404()
    {
        var chat = NewChat();
        Assert.Equal(HttpStatusCode.NotFound, (await WebhookAsync("/stop", chat, header: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await WebhookAsync("/stop", chat, header: "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await WebhookAsync("/stop", chat, secret: "wrong-secret")).StatusCode);
        Assert.Empty(factory.Telegram.To(chat));
    }

    [Fact]
    public async Task Unlink_FromCabinet_And_LinkWithoutAuth_Is401()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today.AddDays(20));
        await LinkChatAsync(b.UserId, NewChat());
        Assert.Equal(HttpStatusCode.NoContent, (await b.Client.DeleteAsync("/api/v1/profile/telegram")).StatusCode);
        Assert.Null(await factory.WithDbAsync(db => db.Users.Where(u => u.Id == b.UserId).Select(u => u.TelegramChatId).SingleAsync()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsync("/api/v1/profile/telegram/link", null)).StatusCode);
    }

    // ---------- доставка: бот заблокирован, повтор ----------

    [Fact]
    public async Task BotBlocked_Unlinks_AndStopsSending()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today);
        var chat = NewChat();
        await LinkChatAsync(b.UserId, chat);
        factory.Telegram.Blocked[chat] = true;

        await RunAsync(s => s.RemindDueTodayAsync());

        var telegram = Assert.Single(await MessagesAsync(b.Deal.DealId), m => m.Channel == BuyerChannels.Telegram);
        Assert.Equal(BuyerMessageStatus.Skipped, telegram.Status);
        Assert.Null(await factory.WithDbAsync(db => db.Users.Where(u => u.Id == b.UserId).Select(u => u.TelegramChatId).SingleAsync()));
    }

    [Fact]
    public async Task TelegramDown_RetriedByJob_ThenSent()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today);
        var chat = NewChat();
        await LinkChatAsync(b.UserId, chat);
        factory.Telegram.FailNext(1);

        await RunAsync(s => s.RemindDueTodayAsync());
        var pending = Assert.Single(await MessagesAsync(b.Deal.DealId), m => m.Channel == BuyerChannels.Telegram);
        Assert.Equal((BuyerMessageStatus.Pending, 1), (pending.Status, pending.Attempts));
        Assert.Contains(pending.Id, factory.BuyerQueue.Retried);

        // Hangfire повторяет задачу — со второй попытки ушло.
        await factory.WithServiceAsync<BuyerMessageJob>(job => job.SendAsync(pending.Id, CancellationToken.None));
        var sent = Assert.Single(await MessagesAsync(b.Deal.DealId), m => m.Channel == BuyerChannels.Telegram);
        Assert.Equal((BuyerMessageStatus.Sent, 2), (sent.Status, sent.Attempts));
        Assert.Single(factory.Telegram.To(chat));
    }

    // ---------- CRM: «Напомнить сейчас» и журнал ----------

    [Fact]
    public async Task RemindNow_OncePerDay_JournalShowsWhoAndChannel()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today.AddDays(10));
        var accountant = await factory.CreateAccountantClientAsync();
        var due = TestScenarios.Today.AddDays(10);

        var first = await accountant.PostAsJsonAsync($"/api/v1/deals/{b.Deal.DealId}/reminders", new RemindNowRequest(due));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(2, (await first.Content.ReadFromJsonAsync<RemindNowResponse>())!.Queued);

        var again = await accountant.PostAsJsonAsync($"/api/v1/deals/{b.Deal.DealId}/reminders", new RemindNowRequest(due));
        Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
        Assert.Contains("reminder_too_soon", await again.Content.ReadAsStringAsync());

        var paid = await accountant.PostAsJsonAsync($"/api/v1/deals/{b.Deal.DealId}/reminders", new RemindNowRequest(due.AddDays(-1)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, paid.StatusCode);

        var journal = (await accountant.GetFromJsonAsync<List<ReminderLogItem>>($"/api/v1/deals/{b.Deal.DealId}/reminders", TestJson.Options))!;
        Assert.Contains(journal, j => j is { Event: BuyerEvents.PaymentManual, Channel: BuyerChannels.Email, Status: BuyerMessageStatus.Sent });
        Assert.All(journal.Where(j => j.Event == BuyerEvents.PaymentManual), j => Assert.False(string.IsNullOrEmpty(j.CreatedBy)));

        // Покупатель журнал CRM не видит.
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync($"/api/v1/deals/{b.Deal.DealId}/reminders")).StatusCode);
    }

    // ---------- события бота ----------

    [Fact]
    public async Task BotEvents_StagePaymentDocumentChat_AndCheckboxes()
    {
        var b = await BuyerWithItemDueAsync(TestScenarios.Today.AddDays(20));
        var chat = NewChat();
        await LinkChatAsync(b.UserId, chat);
        var manager = await factory.CreateManagerClientAsync();
        var accountant = await factory.CreateAccountantClientAsync();

        Assert.True((await manager.PatchAsJsonAsync($"/api/v1/deals/{b.Deal.DealId}/stage", new ChangeDealStageRequest(DealStage.Payment, null), TestJson.Options)).IsSuccessStatusCode);
        Assert.Contains(factory.Telegram.To(chat), m => m.StartsWith("📋 *Сделка: новый этап*\nПо кв. №") && m.Contains("оплата по графику"));

        Assert.True((await TestScenarios.PayAsync(accountant, b.Deal.DealId, 1500m)).IsSuccessStatusCode);
        Assert.Contains(factory.Telegram.To(chat), m => m.StartsWith("✅ *Платёж принят*\n1 500 TJS по кв. №"));

        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var upload = await manager.PostAsync($"/api/v1/deals/{b.Deal.DealId}/documents",
            new MultipartFormDataContent { { file, "file", "Паспорт-скан.pdf" }, { new StringContent(nameof(DocumentType.Contract)), "type" } });
        Assert.True(upload.IsSuccessStatusCode);
        var document = Assert.Single(factory.Telegram.To(chat), m => m.StartsWith("📄 *Новый документ*"));
        Assert.DoesNotContain("Паспорт", document);

        Assert.True((await manager.PostAsJsonAsync($"/api/v1/deals/{b.Deal.DealId}/messages", new SendMessageRequest("Секретный текст"))).IsSuccessStatusCode);
        var message = Assert.Single(factory.Telegram.To(chat), m => m.StartsWith("💬 *Новое сообщение*"));
        Assert.DoesNotContain("Секретный", message);

        // Галочка «этапы сделки» снята — об этапе больше не пишем.
        await b.Client.PutAsJsonAsync("/api/v1/profile/notifications",
            new UpdateNotificationSettingsRequest(true, true, new NotificationEventsDto(true, false, true, true, true)));
        var before = factory.Telegram.To(chat).Count;
        await manager.PatchAsJsonAsync($"/api/v1/deals/{b.Deal.DealId}/stage", new ChangeDealStageRequest(DealStage.Contract, null), TestJson.Options);
        Assert.Equal(before, factory.Telegram.To(chat).Count);
    }

    [Fact]
    public async Task BotEvents_BookingCreatedAndReleased_ForCustomerBooking()
    {
        var manager = await factory.CreateManagerClientAsync();
        var deal = await TestScenarios.CreateDealAsync(factory);
        await factory.CreateCustomerClientAsync(deal.Phone);
        var userId = await factory.WithDbAsync(db => db.Customers.Where(c => c.Id == deal.CustomerId).Select(c => c.UserId!.Value).SingleAsync());
        var chat = NewChat();
        await LinkChatAsync(userId, chat);

        var created = await manager.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest(null, null, deal.CustomerId, null));
        Assert.True(created.IsSuccessStatusCode);
        var booking = (await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        Assert.Contains(factory.Telegram.To(chat), m => m.StartsWith("🏠 *Бронь создана*\nБронь кв. №"));

        // За 6 часов до конца — одно напоминание, сколько бы раз ни запускалась задача.
        await factory.WithDbAsync(db => db.Bookings.Where(x => x.Id == booking.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddHours(5))));
        await factory.WithServiceAsync<Sitora.Infrastructure.Jobs.BookingExpiryNoticeJob>(j => j.RunAsync(CancellationToken.None));
        await factory.WithServiceAsync<Sitora.Infrastructure.Jobs.BookingExpiryNoticeJob>(j => j.RunAsync(CancellationToken.None));
        Assert.Single(factory.Telegram.To(chat), m => m.StartsWith("⏳ *Бронь скоро закончится*"));

        Assert.True((await manager.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest("Передумал"))).IsSuccessStatusCode);
        Assert.Contains(factory.Telegram.To(chat), m => m.StartsWith("🔓 *Бронь снята*"));
    }

    [Theory]
    [InlineData(12500, "12 500 TJS")]
    [InlineData(1250.5, "1 250,50 TJS")]
    public void Money_LooksLikeOnTheSite(decimal amount, string expected) =>
        Assert.Equal(expected, BuyerTexts.Money(amount, Currency.TJS));
}

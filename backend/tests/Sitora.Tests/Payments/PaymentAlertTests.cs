using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

public class PaymentAlertTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static DateOnly Today => TestScenarios.Today;

    [Fact]
    public async Task Overdue_NotifiesBuyerAndAccountant_OnceOnly_AndBellShowsIt()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: Today.AddMonths(-2));
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var (customerUser, accountantUser) = await factory.WithDbAsync(async db => (
            await db.Customers.Where(c => c.Id == deal.CustomerId).Select(c => c.UserId!.Value).SingleAsync(),
            await db.Users.Where(u => u.Email == SitoraApiFactory.AccountantEmail).Select(u => u.Id).SingleAsync()));

        Assert.True(await RunAsync(s => s.NotifyOverdueAsync()) > 0);

        var mine = await Notifications(deal.DealId, customerUser);
        Assert.NotEmpty(mine);
        Assert.All(mine, n => Assert.Equal((Notification.InAppChannel, "Просроченный платёж"), (n.Channel, n.Title)));
        Assert.Contains("просрочен на", mine[0].Text);
        Assert.NotEmpty(await Notifications(deal.DealId, accountantUser));

        // Повторный запуск ничего не добавляет.
        var before = await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.DealId == deal.DealId));
        await RunAsync(s => s.NotifyOverdueAsync());
        Assert.Equal(before, await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.DealId == deal.DealId)));

        // Колокольчик покупателя: непрочитанные, отметка прочитанным; чужое уведомление — 404.
        var bell = (await customer.GetFromJsonAsync<NotificationListResponse>("/api/v1/notifications", TestJson.Options))!;
        var overdue = bell.Items.First(n => n.Kind == Notification.KindOverdue && n.DealId == deal.DealId);
        Assert.False(overdue.IsRead);
        Assert.True(bell.UnreadCount >= mine.Count);
        Assert.Equal(HttpStatusCode.NoContent, (await customer.PostAsync($"/api/v1/notifications/{overdue.Id}/read", null)).StatusCode);
        var after = (await customer.GetFromJsonAsync<NotificationListResponse>("/api/v1/notifications", TestJson.Options))!;
        Assert.True(after.Items.Single(n => n.Id == overdue.Id).IsRead);
        Assert.Equal(bell.UnreadCount - 1, after.UnreadCount);

        var accountantNotification = (await Notifications(deal.DealId, accountantUser))[0].Id;
        Assert.Equal(HttpStatusCode.NotFound, (await customer.PostAsync($"/api/v1/notifications/{accountantNotification}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/notifications")).StatusCode);
    }

    [Fact]
    public async Task Reminder_ThreeDaysBeforeDueDate_ForBuyer()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: Today.AddDays(20));
        await factory.CreateCustomerClientAsync(deal.Phone);
        var due = Today.AddDays(PaymentAlertService.ReminderDaysBefore);
        var customerUser = await factory.WithDbAsync(async db =>
        {
            // Первый пункт графика — ровно через 3 дня.
            var item = await db.PaymentScheduleItems.Where(i => i.Plan.DealId == deal.DealId).OrderBy(i => i.DueDate).FirstAsync();
            item.DueDate = due;
            await db.SaveChangesAsync();
            return await db.Customers.Where(c => c.Id == deal.CustomerId).Select(c => c.UserId!.Value).SingleAsync();
        });

        await RunAsync(s => s.RemindUpcomingAsync());
        await RunAsync(s => s.RemindUpcomingAsync());

        var reminder = Assert.Single(await Notifications(deal.DealId, customerUser), n => n.Kind == Notification.KindReminder);
        Assert.Equal(("Напоминание о платеже", due), (reminder.Title, reminder.DueDate));
        Assert.StartsWith($"Через 3 дня ({due:dd.MM.yyyy}) платёж ", reminder.Text);
        // Просрочки по этой сделке нет: все сроки впереди.
        await RunAsync(s => s.NotifyOverdueAsync());
        Assert.DoesNotContain(await Notifications(deal.DealId, customerUser), n => n.Kind == Notification.KindOverdue);
    }

    [Fact]
    public void NbtParser_ReadsUsdFromWindows1251Xml()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var xml = Encoding.GetEncoding(1251).GetBytes("""
            <?xml version="1.0" encoding="windows-1251" ?>
            <ValCurs Date="2026-10-31" name="Котировки валют устанавливаемые ежедневно">
            <Valute ID="978"><CharCode>EUR</CharCode><Nominal>1</Nominal><Name>ЕВРО</Name><Value>10.3118</Value></Valute>
            <Valute ID="840">
               <CharCode>USD</CharCode>
               <Nominal>1</Nominal>
               <Name>Доллар США</Name>
               <Value>9.2086</Value>
            </Valute>
            <Valute ID="643"><CharCode>RUB</CharCode><Nominal>100</Nominal><Name>Российский рубль</Name><Value>11,2350</Value></Valute>
            </ValCurs>
            """);

        Assert.Equal(9.2086m, NbtRateProvider.ParseUsdRate(xml));
        Assert.Equal(0.1124m, NbtRateProvider.ParseUsdRate(Encoding.UTF8.GetBytes(
            "<ValCurs><Valute><CharCode>USD</CharCode><Nominal>100</Nominal><Value>11,2350</Value></Valute></ValCurs>")));
        Assert.Null(NbtRateProvider.ParseUsdRate(Encoding.UTF8.GetBytes("<ValCurs><Valute><CharCode>EUR</CharCode><Value>10</Value></Valute></ValCurs>")));
        // Сайт вместо XML отдал страницу с ошибкой.
        Assert.Null(NbtRateProvider.ParseUsdRate(Encoding.UTF8.GetBytes("При выполнении скрипта возникла ошибка.")));
    }

    private async Task<int> RunAsync(Func<PaymentAlertService, Task<int>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<PaymentAlertService>());
    }

    private Task<List<Notification>> Notifications(Guid dealId, Guid userId) =>
        factory.WithDbAsync(db => db.Notifications.AsNoTracking().Where(n => n.DealId == dealId && n.UserId == userId).ToListAsync());
}

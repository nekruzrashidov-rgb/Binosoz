using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

// Telegram — в дополнение к колокольчику: сообщение получают только те, кто привязал чат, и только по своему дому и роли.
public class TelegramAlertTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Payment_GoesToOwnerAndAccountant_BookingToOwnerAndManager()
    {
        var (owner, accountant, manager) = (await factory.CreateAdminClientAsync(), await factory.CreateAccountantClientAsync(), await factory.CreateManagerClientAsync());
        var (ownerChat, accountantChat, managerChat) = (NewChat(), NewChat(), NewChat());
        await LinkAsync(owner, ownerChat);
        await LinkAsync(accountant, accountantChat);
        await LinkAsync(manager, managerChat);

        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        Assert.True((await TestScenarios.PayAsync(accountant, deal.DealId, 1000m)).IsSuccessStatusCode);
        var apartment = await factory.WithDbAsync(db => db.Deals.Where(d => d.Id == deal.DealId).Select(d => d.Apartment.Number).SingleAsync());

        var payment = $"💰 *Новый платёж*\nПокупатель Кабинета, кв. {apartment}, {TelegramMessages.Money(1000m, Currency.TJS)}, {TestScenarios.Today:dd.MM.yyyy}";
        Assert.Contains(payment, factory.Telegram.To(ownerChat));
        Assert.Contains(payment, factory.Telegram.To(accountantChat));
        Assert.DoesNotContain(factory.Telegram.To(managerChat), m => m.StartsWith("💰"));

        // Бронь менеджером: владелец и менеджер, бухгалтеру — нет.
        Assert.True((await manager.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость Телеграма", SitoraApiFactory.RandomPhone(), null, null))).IsSuccessStatusCode);
        Assert.Contains(factory.Telegram.To(managerChat), m => m.StartsWith("🏠 *Новая бронь*\nКв. ") && m.Contains("Гость Телеграма"));
        Assert.Contains(factory.Telegram.To(ownerChat), m => m.StartsWith("🏠 *Новая бронь*"));
        Assert.DoesNotContain(factory.Telegram.To(accountantChat), m => m.StartsWith("🏠"));
    }

    [Fact]
    public async Task Overdue_ToBuyerWithChat_Once_AndUnlinkedGetsNothing()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: TestScenarios.Today.AddMonths(-2));
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var chat = NewChat();
        await LinkAsync(customer, chat);

        await RunOverdueAsync();
        await RunOverdueAsync();

        var overdue = factory.Telegram.To(chat).Where(m => m.StartsWith("🔴 *Просрочка*\nПокупатель Кабинета, кв. ")).ToList();
        Assert.NotEmpty(overdue);
        // Повторный запуск — те же пункты графика, второго сообщения нет.
        Assert.Equal(overdue.Distinct().Count(), overdue.Count);

        // Отвязал чат — новые сообщения не приходят.
        var before = factory.Telegram.To(chat).Count;
        await LinkAsync(customer, null);
        var other = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, other.DealId, start: TestScenarios.Today.AddMonths(-2));
        await RunOverdueAsync();
        Assert.Equal(before, factory.Telegram.To(chat).Count);
    }

    [Fact]
    public async Task Profile_ZeroChatIs422_AnonymousIs401()
    {
        var manager = await factory.CreateManagerClientAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await manager.PostAsJsonAsync("/api/v1/profile/telegram", new TelegramChatRequest(0))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/v1/profile/telegram", new TelegramChatRequest(1))).StatusCode);
        Assert.Equal($"🟡 *Напоминание*\nПлатёж {TelegramMessages.Money(1500m, Currency.TJS)} через 3 дня, 05.12.2026",
            TelegramMessages.Reminder(1500m, Currency.TJS, new DateOnly(2026, 12, 5)));
        Assert.EndsWith("500,00 TJS", TelegramMessages.Money(1500m, Currency.TJS));
        Assert.Equal("🏠 *Новая бронь*\nКв. 7, Иван\\_Петров, 10,00 USD", TelegramMessages.Booking(7, "Иван_Петров", 10m, Currency.USD));
    }

    private static long NewChat() => Random.Shared.NextInt64(1_000_000, long.MaxValue);

    private static async Task LinkAsync(HttpClient client, long? chat) =>
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/profile/telegram", new TelegramChatRequest(chat))).StatusCode);

    private async Task RunOverdueAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PaymentAlertService>().NotifyOverdueAsync();
    }
}

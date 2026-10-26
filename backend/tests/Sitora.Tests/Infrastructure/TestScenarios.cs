using System.Net.Http.Json;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;

namespace Sitora.Tests.Infrastructure;

public record DealScenario(Guid DealId, Guid CustomerId, string Phone, decimal Price);

public static class TestScenarios
{
    public static DateOnly Today => DushanbeTime.Today(DateTime.UtcNow);

    // Новый клиент, свободная квартира и сделка на этапе «Договор».
    public static async Task<DealScenario> CreateDealAsync(SitoraApiFactory factory, Currency currency = Currency.TJS)
    {
        var manager = await factory.CreateManagerClientAsync();
        var phone = SitoraApiFactory.RandomPhone();
        var customer = await ReadAsync<CustomerResponse>(await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Покупатель Кабинета", phone, null, null, null)));
        var deal = await ReadAsync<DealResponse>(await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(await factory.FindFreeApartmentAsync(), customer.Id, DealStage.Contract, currency, null, null),
            TestJson.Options));
        return new DealScenario(deal.Id, customer.Id, phone, deal.FinalPrice);
    }

    public static async Task<ScheduleResponse> CreatePlanAsync(SitoraApiFactory factory, Guid dealId,
        decimal downPct = 20, int months = 10, int day = 5, DateOnly? start = null)
    {
        var manager = await factory.CreateManagerClientAsync();
        return await ReadAsync<ScheduleResponse>(await manager.PostAsJsonAsync($"/api/v1/deals/{dealId}/plan",
            new CreatePlanRequest(downPct, months, day, start ?? Today.AddMonths(-2))));
    }

    public static async Task<HttpResponseMessage> PayAsync(HttpClient accountant, Guid dealId, decimal amount,
        Currency currency = Currency.TJS, DateOnly? paidAt = null) =>
        await accountant.PostAsJsonAsync($"/api/v1/deals/{dealId}/payments",
            new CreatePaymentRequest(amount, currency, paidAt ?? Today, PaymentMethod.BankTransfer, null), TestJson.Options);

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }
}

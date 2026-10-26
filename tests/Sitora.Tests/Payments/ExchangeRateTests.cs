using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Jobs;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

// Все проверки курса в одном тесте-классе на свою базу: курсы общие для всех сделок.
public class ExchangeRateTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static readonly DateOnly Today = TestScenarios.Today;

    [Fact]
    public async Task UsdDeal_PricesAndPaymentsAreRecalculatedByRateOnPaymentDate()
    {
        var accountant = await factory.CreateAccountantClientAsync();

        // Без курса сделку в долларах не оформить.
        var withoutRate = await CreateUsdDealResponseAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutRate.StatusCode);

        await SetRateAsync(accountant, Today.AddDays(-10), 10.5m);
        await SetRateAsync(accountant, Today, 11m);

        // Ручной курс бухгалтера действует в его организации: публичный сайт застройщика передаёт её id,
        // а без него виден только общий курс НБТ (его ещё нет).
        var current = await factory.CreateClient().GetFromJsonAsync<RateResponse>(
            $"/api/v1/rates/current?organizationId={Organization.DefaultId}", TestJson.Options);
        Assert.Equal((Today, 11m, RateSource.Manual), (current!.Date, current.RateToTjs, current.Source));
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/api/v1/rates/current")).StatusCode);
        var bySlug = await factory.CreateClient().GetFromJsonAsync<RateResponse>(
            $"/api/v1/rates/current?organization={Organization.DefaultSlug}", TestJson.Options);
        Assert.Equal(11m, bySlug!.RateToTjs);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/api/v1/rates/current?organization=no-such-org")).StatusCode);

        var dealResponse = await CreateUsdDealResponseAsync();
        var deal = await TestScenarios.ReadAsync<DealResponse>(dealResponse);
        var apartment = await factory.CreateClient().GetFromJsonAsync<ApartmentResponse>($"/api/v1/apartments/{deal.ApartmentId}", TestJson.Options);
        Assert.Equal(Currency.USD, deal.Currency);
        Assert.Equal(Math.Round(apartment!.Price / 11m, 0, MidpointRounding.AwayFromZero), deal.Price);

        await TestScenarios.CreatePlanAsync(factory, deal.Id, start: Today.AddMonths(-1));

        // Платёж в сомони пять дней назад: берётся последний курс не позже этой даты (10,5).
        var inTjs = await TestScenarios.ReadAsync<PaymentResponse>(
            await TestScenarios.PayAsync(accountant, deal.Id, 1000m, Currency.TJS, Today.AddDays(-5)));
        Assert.Equal(10.5m, inTjs.RateUsed);
        Assert.Equal(95.24m, inTjs.AmountInDealCurrency);
        Assert.Equal(1000m, inTjs.AmountTjs);

        // Платёж в долларах сегодня: сумма в сомони по курсу 11.
        var inUsd = await TestScenarios.ReadAsync<PaymentResponse>(
            await TestScenarios.PayAsync(accountant, deal.Id, 100m, Currency.USD, Today));
        Assert.Equal(11m, inUsd.RateUsed);
        Assert.Equal(100m, inUsd.AmountInDealCurrency);
        Assert.Equal(1100m, inUsd.AmountTjs);

        // Раньше первого курса пересчитать нельзя.
        var tooEarly = await TestScenarios.PayAsync(accountant, deal.Id, 100m, Currency.TJS, Today.AddDays(-30));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooEarly.StatusCode);
        Assert.Contains("Нет курса USD", await tooEarly.Content.ReadAsStringAsync());

        var schedule = await accountant.GetFromJsonAsync<ScheduleResponse>($"/api/v1/deals/{deal.Id}/schedule", TestJson.Options);
        Assert.Equal(195.24m, schedule!.Paid);

        // Сторно повторяет курс исходного платежа.
        var reversal = await TestScenarios.ReadAsync<PaymentResponse>(await accountant.PostAsJsonAsync(
            $"/api/v1/payments/{inTjs.Id}/reverse", new ReversePaymentRequest("Ошибка")));
        Assert.Equal((10.5m, -95.24m, -1000m), (reversal.RateUsed, reversal.AmountInDealCurrency, reversal.AmountTjs));
    }

    [Fact]
    public async Task SetRate_OnlyAccountantOrAdmin_AndValidates()
    {
        var request = new SetRateRequest(Today.AddDays(-1), Currency.USD, 10.9m);

        var manager = await (await factory.CreateManagerClientAsync()).PutAsJsonAsync("/api/v1/rates", request, TestJson.Options);
        var guest = await factory.CreateClient().PutAsJsonAsync("/api/v1/rates", request, TestJson.Options);
        var admin = await (await factory.CreateAdminClientAsync()).PutAsJsonAsync("/api/v1/rates", request, TestJson.Options);
        var tjs = await (await factory.CreateAdminClientAsync()).PutAsJsonAsync("/api/v1/rates", request with { Currency = Currency.TJS }, TestJson.Options);
        var future = await (await factory.CreateAdminClientAsync()).PutAsJsonAsync("/api/v1/rates", request with { Date = Today.AddDays(5) }, TestJson.Options);

        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tjs.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, future.StatusCode);
    }

    // Сообщение по-русски у каждой границы диапазона и при любом языке запроса, без имён полей вроде «Rate To Tjs».
    [Theory]
    [InlineData("ru")]
    [InlineData("tg")]
    public async Task SetRate_ZeroRate_ExplainsInRussian(string language)
    {
        var admin = await factory.CreateAdminClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/rates")
        {
            Content = JsonContent.Create(new SetRateRequest(Today, Currency.USD, 0m), options: TestJson.Options)
        };
        request.Headers.AcceptLanguage.ParseAdd(language);

        var response = await admin.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Курс должен быть больше 0", body);
        Assert.DoesNotContain("Rate To Tjs", body);
    }

    [Fact]
    public async Task UpdateRatesJob_WithStubProvider_KeepsManualRates()
    {
        var before = await factory.WithDbAsync(db => db.ExchangeRates.CountAsync());

        await factory.WithServiceAsync<UpdateRatesJob>(job => job.RunAsync(CancellationToken.None));

        Assert.Equal(before, await factory.WithDbAsync(db => db.ExchangeRates.CountAsync()));
    }

    internal static async Task SetRateAsync(HttpClient accountant, DateOnly date, decimal rate)
    {
        var response = await accountant.PutAsJsonAsync("/api/v1/rates", new SetRateRequest(date, Currency.USD, rate), TestJson.Options);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> CreateUsdDealResponseAsync() => CreateUsdDealResponseAsync(factory);

    internal static async Task<HttpResponseMessage> CreateUsdDealResponseAsync(SitoraApiFactory factory)
    {
        var manager = await factory.CreateManagerClientAsync();
        var customer = await TestScenarios.ReadAsync<CustomerResponse>(await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Долларовый Покупатель", SitoraApiFactory.RandomPhone(), null, null, null)));
        return await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(await factory.FindFreeApartmentAsync(), customer.Id, DealStage.Contract, Currency.USD, null, null),
            TestJson.Options);
    }
}

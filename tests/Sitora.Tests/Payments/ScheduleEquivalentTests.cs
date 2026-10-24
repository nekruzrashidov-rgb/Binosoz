using System.Net.Http.Json;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

// Своя база: тест задаёт курс, а курсы общие для всех сделок.
public class ScheduleEquivalentTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static readonly DateOnly Today = TestScenarios.Today;

    [Fact]
    public async Task Schedule_ShowsEstimatedEquivalentInSecondCurrency_ByCurrentRate()
    {
        var accountant = await factory.CreateAccountantClientAsync();
        await ExchangeRateTests.SetRateAsync(accountant, Today.AddDays(-3), 10m);
        await ExchangeRateTests.SetRateAsync(accountant, Today, 11m);

        // Договор в сомони — эквивалент в долларах по последнему курсу (11, не 10).
        var tjsDeal = await TestScenarios.CreateDealAsync(factory);
        var tjs = await TestScenarios.CreatePlanAsync(factory, tjsDeal.DealId);
        Assert.NotNull(tjs.Equivalent);
        Assert.Equal((Currency.USD, 11m, Today, true),
            (tjs.Equivalent!.Currency, tjs.Equivalent.Rate, tjs.Equivalent.RateDate, tjs.Equivalent.IsEstimate));
        Assert.Contains("Оценочно", tjs.Equivalent.Note);
        Assert.Equal(Usd(tjs.Total), tjs.Equivalent.Total);
        Assert.All(tjs.Items, i => Assert.Equal(Usd(i.AmountDue), i.AmountDueEquivalent));

        // Договор в долларах — эквивалент в сомони.
        var usdDeal = await TestScenarios.ReadAsync<DealResponse>(await ExchangeRateTests.CreateUsdDealResponseAsync(factory));
        var usd = await TestScenarios.CreatePlanAsync(factory, usdDeal.Id);
        Assert.Equal(Currency.TJS, usd.Equivalent!.Currency);
        Assert.Equal(usd.Total * 11m, usd.Equivalent.Total);
        Assert.All(usd.Items, i => Assert.Equal(i.AmountDue * 11m, i.AmountDueEquivalent));

        // После платежа пересчитываются эквиваленты оплаченного и остатка.
        var payment = usd.Items[0].AmountDue;
        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, usdDeal.Id, payment, Currency.USD, Today));
        var afterPayment = await accountant.GetFromJsonAsync<ScheduleResponse>($"/api/v1/deals/{usdDeal.Id}/schedule", TestJson.Options);
        Assert.Equal(payment * 11m, afterPayment!.Equivalent!.Paid);
        Assert.Equal((usd.Total - payment) * 11m, afterPayment.Equivalent.Remaining);
        Assert.Equal(payment * 11m, afterPayment.Items[0].AmountPaidEquivalent);

        // Покупатель видит тот же эквивалент в кабинете.
        var customer = await factory.CreateCustomerClientAsync(tjsDeal.Phone);
        var forCustomer = await customer.GetFromJsonAsync<ScheduleResponse>($"/api/v1/deals/{tjsDeal.DealId}/schedule", TestJson.Options);
        Assert.Equal(tjs.Equivalent.Total, forCustomer!.Equivalent!.Total);
    }

    private static decimal Usd(decimal tjs) => Math.Round(tjs / 11m, 2, MidpointRounding.AwayFromZero);
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

// Имитация банка (MockPaymentGateway) и онлайн-оплата картой через API.
public class MockPaymentGatewayTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private MockPaymentGateway Gateway => (MockPaymentGateway)factory.Services.GetRequiredService<IPaymentGateway>();

    [Fact]
    public void Registered_AsSingletonMock()
    {
        Assert.Equal("Mock", Gateway.ProviderName);
        Assert.Same(factory.Services.GetRequiredService<IPaymentGateway>(), factory.Services.GetRequiredService<IPaymentGateway>());
    }

    [Fact]
    public async Task Initiate_ReturnsUrlWithParameters()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);

        var result = await Gateway.InitiateAsync(new PaymentInitRequest(deal.DealId, 50000.5m, "TJS", "Тест", "/cabinet?success=1", "/api/v1/payment/webhook/mock"));

        Assert.True(result.Success);
        Assert.StartsWith("mock_", result.GatewayPaymentId);
        Assert.StartsWith("/payment/mock-card?", result.PaymentUrl);
        Assert.Contains($"id={result.GatewayPaymentId}", result.PaymentUrl);
        Assert.Contains("amount=50000.5", result.PaymentUrl);
        Assert.Contains("currency=TJS", result.PaymentUrl);
        Assert.Contains("returnUrl=" + Uri.EscapeDataString("/cabinet?success=1"), result.PaymentUrl);
        Assert.False((await Gateway.InitiateAsync(new PaymentInitRequest(deal.DealId, 0, "TJS", "Ноль", "/", "/"))).Success);
    }

    [Fact]
    public async Task Status_IsPendingAfterCreation()
    {
        var id = await NewGatewayPaymentAsync(1000m);

        var status = await Gateway.CheckStatusAsync(id);

        Assert.Equal((PaymentGatewayStatus.Pending, 1000m, "TJS", (DateTime?)null), (status.Status, status.Amount, status.Currency, status.PaidAt));
    }

    [Fact]
    public async Task Simulate_Success_MakesPaid_AndIsAudited()
    {
        var id = await NewGatewayPaymentAsync(1000m);

        var result = await Gateway.SimulatePaymentAsync(id, true);

        Assert.Equal(PaymentGatewayStatus.Paid, result.Status);
        Assert.NotNull((await Gateway.CheckStatusAsync(id)).PaidAt);
        Assert.Contains(await AuditAfterAsync("PaymentGateway", "SimulatedPaid"), after => after.Contains(id));
        // Решённый платёж второй раз не обрабатывается.
        await Assert.ThrowsAsync<BusinessRuleException>(() => Gateway.SimulatePaymentAsync(id, false));
    }

    [Fact]
    public async Task Simulate_Failure_MakesFailed()
    {
        var id = await NewGatewayPaymentAsync(1000m);

        await Gateway.SimulatePaymentAsync(id, false);

        Assert.Equal(PaymentGatewayStatus.Failed, (await Gateway.CheckStatusAsync(id)).Status);
    }

    [Fact]
    public async Task Refund_MakesRefunded_SecondRefundFails_PendingCannotBeRefunded()
    {
        var pending = await NewGatewayPaymentAsync(700m);
        Assert.False((await Gateway.RefundAsync(pending, 700m)).Success);

        var id = await NewGatewayPaymentAsync(700m);
        await Gateway.SimulatePaymentAsync(id, true);

        var refund = await Gateway.RefundAsync(id, 700m);
        Assert.True(refund.Success);
        Assert.Equal(PaymentGatewayStatus.Refunded, (await Gateway.CheckStatusAsync(id)).Status);
        Assert.Contains(await AuditAfterAsync("PaymentGateway", "Refunded"), after => after.Contains("Сторно платежа " + id));

        var again = await Gateway.RefundAsync(id, 700m);
        Assert.False(again.Success);
        Assert.Equal("Платёж уже возвращён", again.ErrorMessage);
    }

    [Fact]
    public async Task UnknownPayment_StatusThrowsNotFound_RefundFails()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Gateway.CheckStatusAsync("mock_" + Guid.NewGuid()));
        Assert.False((await Gateway.RefundAsync("mock_" + Guid.NewGuid(), 1m)).Success);
    }

    [Fact]
    public async Task Api_Initiate_Simulate_Status_CreatesCardPaymentOnce()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var schedule = await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var next = schedule.Items.OrderBy(i => i.DueDate).First();

        var init = await TestScenarios.ReadAsync<InitiatePaymentResponse>(await customer.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/payment/initiate", new InitiatePaymentRequest()));
        Assert.Equal((next.AmountDue, "Mock"), (init.Amount, init.Provider));
        Assert.Contains("returnUrl=" + Uri.EscapeDataString("/cabinet?success=1"), init.PaymentUrl);
        Assert.Equal(PaymentGatewayStatus.Pending, (await Status(customer, deal.DealId, init.GatewayPaymentId)).Status);
        Assert.Equal(0, await PaymentsCountAsync(deal.DealId));

        // «Банк» — анонимная страница, как настоящий.
        (await factory.CreateClient().PostAsJsonAsync("/api/v1/payment/mock/simulate", new SimulatePaymentRequest(init.GatewayPaymentId, true))).EnsureSuccessStatusCode();

        var paid = await Status(customer, deal.DealId, init.GatewayPaymentId);
        Assert.Equal(PaymentGatewayStatus.Paid, paid.Status);
        Assert.NotNull(paid.PaymentId);
        // Повторные запросы статуса второй платёж не создают.
        await Status(customer, deal.DealId, init.GatewayPaymentId);
        Assert.Equal(1, await PaymentsCountAsync(deal.DealId));
        var payment = await factory.WithDbAsync(db => db.Payments.IgnoreQueryFilters().SingleAsync(p => p.DealId == deal.DealId));
        Assert.Equal((PaymentMethod.Card, next.AmountDue), (payment.Method, payment.Amount));
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.Entity == "OnlinePayment" && l.Action == "Paid")));
    }

    [Fact]
    public async Task Api_Access_Validation_AndWebhookStub()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var foreign = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(foreign.Phone);

        // Чужая сделка — 404; без графика — 404; сумма больше остатка — 422; чужой адрес возврата заменяется на кабинет.
        Assert.Equal(HttpStatusCode.NotFound, (await customer.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/payment/initiate", new InitiatePaymentRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.PostAsJsonAsync($"/api/v1/deals/{foreign.DealId}/payment/initiate", new InitiatePaymentRequest())).StatusCode);
        var accountant = await factory.CreateAccountantClientAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await accountant.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/payment/initiate", new InitiatePaymentRequest(999_999_999m))).StatusCode);
        var staff = await TestScenarios.ReadAsync<InitiatePaymentResponse>(await accountant.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/payment/initiate",
            new InitiatePaymentRequest(1000m, "https://evil.example/steal")));
        Assert.Contains("returnUrl=" + Uri.EscapeDataString("/cabinet?success=1"), staff.PaymentUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/v1/deals/{deal.DealId}/payment/{staff.GatewayPaymentId}/status")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/payment/initiate", new InitiatePaymentRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.NotImplemented, (await factory.CreateClient().PostAsync("/api/v1/payment/webhook/alif", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/payment/mock-card?id=x")).StatusCode);
    }

    private async Task<string> NewGatewayPaymentAsync(decimal amount)
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        return (await Gateway.InitiateAsync(new PaymentInitRequest(deal.DealId, amount, "TJS", "Тест", "/cabinet?success=1", "/api/v1/payment/webhook/mock"))).GatewayPaymentId!;
    }

    private static async Task<OnlinePaymentStatusResponse> Status(HttpClient client, Guid dealId, string id) =>
        await TestScenarios.ReadAsync<OnlinePaymentStatusResponse>(await client.GetAsync($"/api/v1/deals/{dealId}/payment/{id}/status"));

    // Тексты «после» записей журнала с этим действием (jsonb — сравниваем в памяти).
    private async Task<List<string>> AuditAfterAsync(string entity, string action) =>
        (await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().Where(l => l.Entity == entity && l.Action == action && l.After != null).Select(l => l.After!).ToListAsync()));

    private Task<int> PaymentsCountAsync(Guid dealId) =>
        factory.WithDbAsync(db => db.Payments.IgnoreQueryFilters().CountAsync(p => p.DealId == dealId));
}

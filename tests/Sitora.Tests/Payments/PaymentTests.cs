using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

public class PaymentTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Plan_IsBuiltFromDealPrice()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);

        var schedule = await TestScenarios.CreatePlanAsync(factory, deal.DealId, downPct: 20, months: 10);

        Assert.Equal(deal.Price, schedule.Total);
        Assert.Equal(11, schedule.Items.Count);
        Assert.Equal(Math.Round(deal.Price * 0.2m, 0, MidpointRounding.AwayFromZero), schedule.Items[0].AmountDue);
        Assert.Equal(0m, schedule.Paid);
        Assert.Equal(deal.Price, schedule.Remaining);
        // График начат два месяца назад: взнос и первый месяц уже просрочены.
        Assert.True(schedule.Overdue > 0);

        // Курс в этой базе не вводили — эквивалента в долларах нет.
        Assert.Null(schedule.Equivalent);
        Assert.All(schedule.Items, i => Assert.Null(i.AmountDueEquivalent));
    }

    [Fact]
    public async Task Payment_ClosesOldestItems_AndIsVisibleToCustomer()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var plan = await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var accountant = await factory.CreateAccountantClientAsync();
        var down = plan.Items[0].AmountDue;
        var monthly = plan.Items[1].AmountDue;

        var response = await TestScenarios.PayAsync(accountant, deal.DealId, down + monthly / 2);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await TestScenarios.ReadAsync<PaymentResponse>(response);
        Assert.Null(payment.RateUsed);
        Assert.Equal(payment.Amount, payment.AmountTjs);

        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var schedule = await customer.GetFromJsonAsync<ScheduleResponse>($"/api/v1/deals/{deal.DealId}/schedule", TestJson.Options);
        Assert.Equal(ScheduleItemStatus.Paid, schedule!.Items[0].Status);
        Assert.Equal(ScheduleItemStatus.PartiallyPaid, schedule.Items[1].Status);
        Assert.Equal(monthly / 2, schedule.Items[1].AmountPaid);
        Assert.Equal(ScheduleItemStatus.Pending, schedule.Items[2].Status);
        Assert.Equal(down + monthly / 2, schedule.Paid);
        Assert.Single(schedule.Payments);
    }

    [Fact]
    public async Task Overpayment_AndFutureDate_AreRejected()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var accountant = await factory.CreateAccountantClientAsync();

        var overpay = await TestScenarios.PayAsync(accountant, deal.DealId, deal.Price + 1);
        var future = await TestScenarios.PayAsync(accountant, deal.DealId, 1000m, paidAt: TestScenarios.Today.AddDays(2));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, overpay.StatusCode);
        Assert.Contains("больше остатка", await overpay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, future.StatusCode);
    }

    [Fact]
    public async Task Reversal_CreatesOppositeRecord_RestoresSchedule_AndOnlyOnce()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var plan = await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var accountant = await factory.CreateAccountantClientAsync();
        var payment = await TestScenarios.ReadAsync<PaymentResponse>(
            await TestScenarios.PayAsync(accountant, deal.DealId, plan.Items[0].AmountDue));

        var noReason = await accountant.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/reverse", new ReversePaymentRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var reversed = await accountant.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/reverse", new ReversePaymentRequest("Ошибка в сумме"));
        Assert.Equal(HttpStatusCode.Created, reversed.StatusCode);
        var reversal = await TestScenarios.ReadAsync<PaymentResponse>(reversed);
        Assert.Equal(-payment.Amount, reversal.Amount);
        Assert.Equal(payment.Id, reversal.ReversalOfId);

        var again = await accountant.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/reverse", new ReversePaymentRequest("ещё раз"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var ofReversal = await accountant.PostAsJsonAsync($"/api/v1/payments/{reversal.Id}/reverse", new ReversePaymentRequest("сторно сторно"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ofReversal.StatusCode);

        var schedule = await accountant.GetFromJsonAsync<ScheduleResponse>($"/api/v1/deals/{deal.DealId}/schedule", TestJson.Options);
        Assert.Equal(0m, schedule!.Paid);
        Assert.All(schedule.Items, i => Assert.Equal(ScheduleItemStatus.Pending, i.Status));
        Assert.True(schedule.Payments.Single(p => p.Id == payment.Id).IsReversed);

        // Оба платежа на месте: ничего не удалено.
        var rows = await factory.WithDbAsync(db => db.Payments.CountAsync(p => p.DealId == deal.DealId));
        Assert.Equal(2, rows);
        var audit = await factory.WithDbAsync(db => db.AuditLogs.CountAsync(a => a.EntityId == payment.Id));
        Assert.Equal(2, audit);
    }

    [Fact]
    public async Task Plan_CannotBeReplacedAfterPayments()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var replaced = await TestScenarios.CreatePlanAsync(factory, deal.DealId, downPct: 50, months: 6);
        Assert.Equal(7, replaced.Items.Count);

        var accountant = await factory.CreateAccountantClientAsync();
        (await TestScenarios.PayAsync(accountant, deal.DealId, 1000m)).EnsureSuccessStatusCode();

        var manager = await factory.CreateManagerClientAsync();
        var response = await manager.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/plan",
            new CreatePlanRequest(10, 12, 5, TestScenarios.Today));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Payments_OnlyForAccountantAndAdmin()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);

        var manager = await TestScenarios.PayAsync(await factory.CreateManagerClientAsync(), deal.DealId, 100m);
        var content = await TestScenarios.PayAsync(await factory.CreateContentClientAsync(), deal.DealId, 100m);
        var admin = await TestScenarios.PayAsync(await factory.CreateAdminClientAsync(), deal.DealId, 100m);

        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, content.StatusCode);
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
    }

    [Fact]
    public async Task PaymentWithoutPlan_Returns422()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);

        var response = await TestScenarios.PayAsync(await factory.CreateAccountantClientAsync(), deal.DealId, 100m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Сначала составьте график", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Seeder_CreatesPlanWithThreePaidItems()
    {
        // Другие тесты класса создают свои графики, поэтому ищем график сделки из сидера.
        var plan = await factory.WithDbAsync(db => db.PaymentPlans.Include(p => p.Items)
            .SingleAsync(p => p.Deal.Customer.Email!.EndsWith("@example.com")));

        Assert.Equal(25, plan.Items.Count);
        Assert.Equal(3, plan.Items.Count(i => i.Status == ScheduleItemStatus.Paid));
        var payments = await factory.WithDbAsync(db => db.Payments.Where(p => p.DealId == plan.DealId).SumAsync(p => p.AmountInDealCurrency));
        Assert.Equal(plan.Items.Where(i => i.Status == ScheduleItemStatus.Paid).Sum(i => i.AmountDue), payments);
    }
}

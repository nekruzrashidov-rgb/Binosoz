using System.Net;
using System.Net.Http.Json;
using Sitora.Domain.DTOs.Payments;
using Sitora.Infrastructure.Integrations;
using Sitora.Tests.Infrastructure;
using UglyToad.PdfPig;

namespace Sitora.Tests.Payments;

public class StatementTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Statement_IsPdf_AndTotalsMatchPayments()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var plan = await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: TestScenarios.Today.AddMonths(-2));
        var accountant = await factory.CreateAccountantClientAsync();
        var down = plan.Items[0];
        var firstMonth = plan.Items[1];

        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, down.AmountDue, paidAt: down.DueDate));
        var mistaken = await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, 777m, paidAt: firstMonth.DueDate));
        await TestScenarios.ReadAsync<PaymentResponse>(await accountant.PostAsJsonAsync(
            $"/api/v1/payments/{mistaken.Id}/reverse", new ReversePaymentRequest("Ошиблись суммой")));
        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, firstMonth.AmountDue, paidAt: firstMonth.DueDate));

        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var response = await customer.GetAsync($"/api/v1/deals/{deal.DealId}/statement.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);

        var schedule = await customer.GetFromJsonAsync<ScheduleResponse>($"/api/v1/deals/{deal.DealId}/schedule", TestJson.Options);
        var paid = down.AmountDue + firstMonth.AmountDue;
        Assert.Equal(paid, schedule!.Paid);

        var text = ExtractText(bytes);
        Assert.Contains("Актсверки", text);
        Assert.Contains("ПокупательКабинета", text);
        Assert.Contains(Compact($"Оплачено: {QuestPdfGenerator.Money(paid)} TJS"), text);
        Assert.Contains(Compact($"Остаток по договору: {QuestPdfGenerator.Money(schedule.Total - paid)} TJS"), text);
        Assert.Contains(Compact($"Сумма договора: {QuestPdfGenerator.Money(schedule.Total)} TJS"), text);
        Assert.Contains("сторно", text);
    }

    [Fact]
    public async Task StatementOnEarlierDate_IncludesOnlyPaymentsUntilThatDate()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var plan = await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: TestScenarios.Today.AddMonths(-2));
        var accountant = await factory.CreateAccountantClientAsync();
        var down = plan.Items[0];
        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, down.AmountDue, paidAt: down.DueDate));
        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, 5000m, paidAt: TestScenarios.Today));

        var asOf = down.DueDate;
        var bytes = await accountant.GetByteArrayAsync($"/api/v1/deals/{deal.DealId}/statement.pdf?date={asOf:yyyy-MM-dd}");

        var text = ExtractText(bytes);
        Assert.Contains(Compact($"на {asOf:dd.MM.yyyy}"), text);
        Assert.Contains(Compact($"Начислено по графику: {QuestPdfGenerator.Money(down.AmountDue)} TJS"), text);
        Assert.Contains(Compact($"Оплачено: {QuestPdfGenerator.Money(down.AmountDue)} TJS"), text);
        Assert.Contains(Compact($"Задолженность: {QuestPdfGenerator.Money(0)} TJS"), text);
    }

    // Пробелы в тексте PDF извлекаются по-разному, поэтому сравниваем без них.
    private static string ExtractText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return Compact(string.Concat(document.GetPages().Select(p => p.Text)));
    }

    private static string Compact(string text) => new(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
}

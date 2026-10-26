using System.Net;
using System.Net.Http.Json;
using Sitora.Domain.DTOs.Payments;
using Sitora.Infrastructure.Integrations;
using Sitora.Tests.Infrastructure;
using UglyToad.PdfPig;

namespace Sitora.Tests.Payments;

public class SchedulePdfTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task SchedulePdf_MatchesScheduleApi_ForOwner()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var plan = await TestScenarios.CreatePlanAsync(factory, deal.DealId, downPct: 20, months: 6);
        var accountant = await factory.CreateAccountantClientAsync();
        await TestScenarios.ReadAsync<PaymentResponse>(
            await TestScenarios.PayAsync(accountant, deal.DealId, plan.Items[0].AmountDue, paidAt: plan.Items[0].DueDate));

        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var response = await customer.GetAsync($"/api/v1/deals/{deal.DealId}/schedule.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);

        var schedule = await customer.GetFromJsonAsync<ScheduleResponse>($"/api/v1/deals/{deal.DealId}/schedule", TestJson.Options);
        var text = ExtractText(bytes);
        Assert.Contains("Графикплатежей", text);
        Assert.Contains("ПокупательКабинета", text);
        Assert.Contains(Compact($"Сумма договора: {QuestPdfGenerator.Money(schedule!.Total)} TJS"), text);
        Assert.Contains(Compact($"Оплачено: {QuestPdfGenerator.Money(schedule.Paid)} TJS"), text);
        Assert.Contains(Compact($"Остаток: {QuestPdfGenerator.Money(schedule.Remaining)} TJS"), text);
        Assert.Contains("оплачен", text);
        foreach (var item in schedule.Items)
            Assert.Contains($"{item.DueDate:dd.MM.yyyy}", text);

        // Курса в этой базе нет — колонки эквивалента и пометки «оценочно» тоже нет.
        Assert.DoesNotContain("Оценочно", text);
    }

    [Fact]
    public async Task SchedulePdf_ForeignDealGives404_AndContentGets403()
    {
        var mine = await TestScenarios.CreateDealAsync(factory);
        var foreign = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, mine.DealId);
        await TestScenarios.CreatePlanAsync(factory, foreign.DealId);
        var customer = await factory.CreateCustomerClientAsync(mine.Phone);

        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/v1/deals/{foreign.DealId}/schedule.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await factory.CreateContentClientAsync()).GetAsync($"/api/v1/deals/{mine.DealId}/schedule.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await (await factory.CreateManagerClientAsync()).GetAsync($"/api/v1/deals/{mine.DealId}/schedule.pdf")).StatusCode);
    }

    internal static string ExtractText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return Compact(string.Concat(document.GetPages().Select(p => p.Text)));
    }

    internal static string Compact(string text) => new(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
}

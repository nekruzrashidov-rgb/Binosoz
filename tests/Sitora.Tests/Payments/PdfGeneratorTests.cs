using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Integrations;
using UglyToad.PdfPig;

namespace Sitora.Tests.Payments;

public class PdfGeneratorTests
{
    [Fact]
    public void Statement_RendersTajikLetters()
    {
        var data = new StatementData(new DateOnly(2026, 10, 24), DateTime.UtcNow, "Sitora Residence", 5, 501,
            "Ҷамшед Ғоибов Қурбонӣ Ҳусейнзода Ӯрунов", Guid.NewGuid(), Currency.TJS, 100000m, 20000m, 20000m, 0m, 80000m,
            [new StatementPayment(new DateOnly(2026, 10, 1), 20000m, Currency.TJS, null, 20000m, false)]);

        var pdf = new QuestPdfGenerator().RenderStatement(data);

        using var document = PdfDocument.Open(pdf);
        var text = string.Concat(document.GetPages().Select(p => p.Text));
        Assert.Contains("ҶамшедҒоибовҚурбонӣҲусейнзодаӮрунов", new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()));
    }

    [Fact]
    public void Schedule_WithEquivalent_ShowsEstimateColumnAndNote()
    {
        var items = new List<ScheduleItemResponse>
        {
            new(Guid.NewGuid(), ScheduleItemKind.DownPayment, new DateOnly(2026, 10, 1), 22000m, 22000m, ScheduleItemStatus.Paid, false, 2000m, 2000m),
            new(Guid.NewGuid(), ScheduleItemKind.Monthly, new DateOnly(2026, 11, 5), 88000m, 0m, ScheduleItemStatus.Pending, false, 8000m, 0m)
        };
        var schedule = new ScheduleResponse(Guid.NewGuid(), Currency.TJS, 110000m, 22000m, 88000m, 0m, 20m, 1, 5,
            new DateOnly(2026, 10, 1), items, [],
            new ScheduleEquivalent(Currency.USD, 11m, new DateOnly(2026, 10, 24), true,
                "Оценочно: по курсу 11,0000 TJS за 1 USD на 24.10.2026.", 10000m, 2000m, 8000m, 0m));
        var data = new ScheduleDocumentData(new DealHeader(schedule.DealId, "Sitora Residence", 5, 501, "Тест Покупатель"),
            schedule, new DateOnly(2026, 10, 24), DateTime.UtcNow);

        var text = SchedulePdfTests.ExtractText(new QuestPdfGenerator().RenderSchedule(data));

        Assert.Contains("≈USD*", text);
        Assert.Contains(SchedulePdfTests.Compact($"Остаток ≈ {QuestPdfGenerator.Money(8000m)} USD"), text);
        Assert.Contains("Оценочно", text);
        Assert.Contains("ожидается", text);
        Assert.Contains("взнос", text);
    }
}

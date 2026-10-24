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
}

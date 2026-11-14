using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Запись о PDF, выданном онлайн (график платежей, акт сверки) — для «Зелёного эффекта». Сбой записи не мешает
// выдать документ: он только пишется в лог.
public class PdfIssueRecorder(AppDbContext db, ILogger<PdfIssueRecorder> logger)
{
    public async Task RecordAsync(Guid dealId, IssuedPdfKind kind, byte[] pdf, CancellationToken ct)
    {
        try
        {
            var organizationId = await db.Deals.IgnoreQueryFilters().Where(d => d.Id == dealId)
                .Select(d => (Guid?)d.Apartment.Floor.Building.OrganizationId).FirstOrDefaultAsync(ct);
            if (organizationId is null)
                return;
            db.IssuedPdfs.Add(new IssuedPdf { OrganizationId = organizationId.Value, DealId = dealId, Kind = kind, PageCount = PdfPages.Count(pdf) });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Не удалось записать выданный PDF {Kind} по сделке {DealId}", kind, dealId);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Settings;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Условия рассрочки застройщика для сайта: минимальный взнос, максимальный срок, «без переплаты».
// Сайт показывает только то, что здесь задано; изменение — в журнал и сброс кэша каталога (условия едут в ответе дома).
public class InstallmentService(AppDbContext db, IAuditService audit, ICurrentUser currentUser, CatalogCache catalogCache) : IInstallmentService
{
    // Вычисляется до запроса (var orgId = OrganizationId): исключение внутри LINQ-выражения EF Core оборачивает
    // в InvalidOperationException, и вместо ответа 400 был 500. Без организации (суперадмин платформы) — 400 organization_required.
    private Guid OrganizationId => currentUser.OrganizationId
        ?? throw new BadRequestException("Укажите организацию");

    public async Task<InstallmentSettingsResponse> GetSettingsAsync(CancellationToken ct = default)
    {
        var orgId = OrganizationId;
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId, ct)
                  ?? throw new NotFoundException("Организация не найдена");
        return ToResponse(org.Installment);
    }

    public async Task<InstallmentSettingsResponse> SaveSettingsAsync(SaveInstallmentSettingsRequest request, CancellationToken ct = default)
    {
        var orgId = OrganizationId;
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct)
                  ?? throw new NotFoundException("Организация не найдена");
        var before = ToResponse(org.Installment);
        org.Installment.DownPaymentMinPct = request.DownPaymentMinPct;
        org.Installment.MaxMonths = request.MaxMonths;
        org.Installment.NoOverpayment = request.NoOverpayment;
        audit.Record("Organization", org.Id, "InstallmentSettingsUpdated", before, ToResponse(org.Installment), org.Id);
        await db.SaveChangesAsync(ct);
        await catalogCache.InvalidateAsync(ct);
        return ToResponse(org.Installment);
    }

    private static InstallmentSettingsResponse ToResponse(OrganizationInstallmentSettings s) =>
        new(s.DownPaymentMinPct, s.MaxMonths, s.NoOverpayment);
}

using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Green;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// «Зелёная» часть платформы. Ничего не выдумывается: тарифы и коэффициенты задаёт застройщик (с источником),
// расчёты делаются только по ним и по проектным данным дома; чего нет — того и в ответе нет.
public partial class GreenService(
    AppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    CatalogCache catalogCache,
    TimeProvider timeProvider) : IGreenService
{
    private Guid OrganizationId => currentUser.OrganizationId
        ?? throw new BusinessRuleException("Выберите организацию: войдите как её владелец");

    public async Task<GreenSettingsResponse> GetSettingsAsync(CancellationToken ct = default)
    {
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == OrganizationId, ct)
                  ?? throw new NotFoundException("Организация не найдена");
        return ToResponse(org.Green);
    }

    public async Task<GreenSettingsResponse> SaveSettingsAsync(SaveGreenSettingsRequest request, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == OrganizationId, ct)
                  ?? throw new NotFoundException("Организация не найдена");
        var before = ToResponse(org.Green);
        org.Green.TariffKwhTjs = request.TariffKwhTjs;
        org.Green.BaselineKwhPerM2Year = request.BaselineKwhPerM2Year;
        org.Green.BaselineSource = Clean(request.BaselineSource);
        org.Green.Co2KgPerKgPaper = request.Co2KgPerKgPaper;
        org.Green.Co2Source = Clean(request.Co2Source);
        org.Green.IsDemo = request.IsDemo;
        audit.Record("Organization", org.Id, "GreenSettingsUpdated", before, ToResponse(org.Green), org.Id);
        await db.SaveChangesAsync(ct);
        // Тариф и признак демо видны в каталоге и в расчёте расходов — сбрасываем кэш каталога.
        await catalogCache.InvalidateAsync(ct);
        return ToResponse(org.Green);
    }

    // Расчёт расходов квартиры: нужен расчётный расход дома и тариф застройщика, иначе null (на сайте не показывается).
    public async Task<UtilityEstimateResponse?> GetUtilityEstimateAsync(Guid apartmentId, CancellationToken ct = default)
    {
        var x = await db.Apartments.AsNoTracking()
                    .Where(a => a.Id == apartmentId && a.Floor.Building.Organization.IsActive)
                    .Select(a => new
                    {
                        a.Area,
                        Kwh = a.Floor.Building.Eco == null ? null : a.Floor.Building.Eco.EstimatedKwhPerM2Year,
                        a.Floor.Building.Organization.Green
                    })
                    .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Квартира не найдена");
        if (x.Kwh is not { } kwh || x.Green.TariffKwhTjs is not { } tariff || x.Area <= 0)
            return null;

        var e = UtilityCalculator.Calculate(x.Area, kwh, tariff, x.Green.BaselineKwhPerM2Year);
        return new UtilityEstimateResponse(apartmentId, x.Area, kwh, tariff, e.KwhPerYear, e.KwhPerMonth, e.CostPerYearTjs,
            e.CostPerMonthTjs, x.Green.BaselineKwhPerM2Year, e.BaselineKwhPerYear, e.BaselineCostPerYearTjs, e.SavingKwhPerYear,
            e.SavingCostPerYearTjs, x.Green.BaselineKwhPerM2Year is null ? null : x.Green.BaselineSource, x.Green.IsDemo);
    }

    // Лист A4 80 г/м² (0,0625 м²) весит 5 г.
    public const decimal PaperKgPerPage = 0.005m;

    public async Task<GreenImpactResponse> GetPublicImpactAsync(string organizationSlug, CancellationToken ct = default)
    {
        var slug = organizationSlug.Trim().ToLowerInvariant();
        var org = await db.Organizations.AsNoTracking().Where(o => o.Slug == slug && o.IsActive)
                      .Select(o => new { o.Id, o.Green }).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Организация не найдена");
        return (await CountAsync(org.Id, org.Green, ct)).Totals;
    }

    public async Task<GreenStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == OrganizationId)
                      .Select(o => new { o.Id, o.Green }).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Организация не найдена");
        var (totals, bookings, payments, chat) = await CountAsync(org.Id, org.Green, ct);

        // Страницы по месяцам за последние 12 месяцев: загруженные PDF и выданные онлайн графики и акты.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
        var docs = await Documents(org.Id).Where(d => d.CreatedAt >= from)
            .Select(d => new { d.CreatedAt, d.PageCount }).ToListAsync(ct);
        var issued = await db.IssuedPdfs.AsNoTracking().Where(x => x.OrganizationId == org.Id && x.CreatedAt >= from)
            .Select(x => new { x.CreatedAt, x.PageCount }).ToListAsync(ct);
        var all = docs.Select(d => (d.CreatedAt, d.PageCount)).Concat(issued.Select(x => (x.CreatedAt, x.PageCount))).ToList();
        var months = Enumerable.Range(0, 12).Select(i => from.AddMonths(i)).Select(m =>
        {
            var inMonth = all.Where(x => x.CreatedAt.Year == m.Year && x.CreatedAt.Month == m.Month).ToList();
            var pages = inMonth.Sum(x => x.PageCount ?? 0);
            return new GreenMonthResponse(m.ToString("yyyy-MM"), inMonth.Count, pages, Math.Round(pages * PaperKgPerPage, 3, MidpointRounding.AwayFromZero));
        }).ToList();
        return new GreenStatsResponse(totals, bookings, payments, chat, months);
    }

    // Документы сделок организации (без фильтров текущего пользователя: публичный агрегат считается и для гостя).
    private IQueryable<Sitora.Domain.Entities.Document> Documents(Guid organizationId) =>
        db.Documents.IgnoreQueryFilters().AsNoTracking().Where(d => d.Deal.Apartment.Floor.Building.OrganizationId == organizationId);

    // Только реальные счётчики. Страницы — из файлов (PDF без определённого числа страниц не считается).
    // Действия без визита в офис: брони с сайта, оплаченные онлайн платежи, сообщения в чате сделки.
    private async Task<(GreenImpactResponse Totals, int Bookings, int Payments, int Chat)> CountAsync(
        Guid organizationId, Sitora.Domain.Entities.OrganizationGreenSettings green, CancellationToken ct)
    {
        var documents = await Documents(organizationId).CountAsync(ct);
        var documentPages = await Documents(organizationId).SumAsync(d => d.PageCount ?? 0, ct);
        var issued = db.IssuedPdfs.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        var issuedCount = await issued.CountAsync(ct);
        var issuedPages = await issued.SumAsync(x => x.PageCount ?? 0, ct);

        var bookings = await db.Bookings.IgnoreQueryFilters().CountAsync(b =>
            b.Apartment.Floor.Building.OrganizationId == organizationId && b.Lead != null && b.Lead.Source == Sitora.Domain.Enums.LeadSource.Website, ct);
        var payments = await db.OnlinePayments.IgnoreQueryFilters().CountAsync(p =>
            p.Deal.Apartment.Floor.Building.OrganizationId == organizationId && p.Status == PaymentGatewayStatus.Paid, ct);
        var chat = await db.ChatMessages.IgnoreQueryFilters().CountAsync(m =>
            m.Deal.Apartment.Floor.Building.OrganizationId == organizationId, ct);

        var pages = documentPages + issuedPages;
        var paperKg = Math.Round(pages * PaperKgPerPage, 2, MidpointRounding.AwayFromZero);
        decimal? co2 = green.Co2KgPerKgPaper is { } k && !string.IsNullOrWhiteSpace(green.Co2Source)
            ? Math.Round(pages * PaperKgPerPage * k, 2, MidpointRounding.AwayFromZero)
            : null;
        var totals = new GreenImpactResponse(documents + issuedCount, pages, paperKg, co2, co2 is null ? null : green.Co2Source,
            bookings + payments + chat, green.IsDemo);
        return (totals, bookings, payments, chat);
    }

    private static GreenSettingsResponse ToResponse(Sitora.Domain.Entities.OrganizationGreenSettings g) =>
        new(g.TariffKwhTjs, g.BaselineKwhPerM2Year, g.BaselineSource, g.Co2KgPerKgPaper, g.Co2Source, g.IsDemo);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

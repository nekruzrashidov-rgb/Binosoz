using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Сводка по всей платформе для суперадмина: у него нет организации, поэтому фильтры AppDbContext показывают всё.
public class PlatformDashboardService(AppDbContext db, TimeProvider timeProvider) : IPlatformDashboardService
{
    private const int TopCount = 5;
    private const int Months = 12;

    public async Task<PlatformDashboardResponse> GetAsync(CancellationToken ct = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var today = DushanbeTime.Today(nowUtc);
        var monthStart = MonthStartUtc(today.Year, today.Month);
        var nextMonth = MonthStartUtc(today.AddMonths(1).Year, today.AddMonths(1).Month);
        var usd = await db.ExchangeRates.AsNoTracking()
            .Where(r => r.OrganizationId == null && r.Currency == Currency.USD)
            .OrderByDescending(r => r.Date).Select(r => (decimal?)r.RateToTjs).FirstOrDefaultAsync(ct) ?? 0m;
        decimal Tjs(decimal amount, Currency currency) => currency == Currency.USD ? Math.Round(amount * usd, 2) : amount;

        var organizations = await db.Organizations.CountAsync(ct);
        var active = await db.Organizations.CountAsync(o => o.IsActive, ct);
        var deals = db.Deals.Where(d => d.Stage != DealStage.Cancelled);

        var revenueRows = await db.OrganizationTariffs.AsNoTracking()
            .Where(t => t.IsPaid && t.StartsAt < nextMonth && (t.EndsAt == null || t.EndsAt > monthStart))
            .Select(t => new { t.TariffPlan.Price, t.TariffPlan.Currency })
            .ToListAsync(ct);

        var top = await db.Organizations.AsNoTracking()
            .Select(o => new
            {
                o.Id, o.Name,
                Deals = deals.Count(d => d.Apartment.Floor.Building.OrganizationId == o.Id),
                Payments = db.Payments.Where(p => p.Deal.Apartment.Floor.Building.OrganizationId == o.Id).Sum(p => (decimal?)p.AmountTjs) ?? 0
            })
            .OrderByDescending(o => o.Deals).ThenByDescending(o => o.Payments).ThenBy(o => o.Name)
            .Take(TopCount)
            .ToListAsync(ct);

        // Последние 12 месяцев, включая текущий; месяцы без сделок — с нулями, чтобы график не прыгал.
        var firstMonth = today.AddMonths(-(Months - 1));
        var since = MonthStartUtc(firstMonth.Year, firstMonth.Month);
        var recent = await deals.AsNoTracking().Where(d => d.CreatedAt >= since)
            .Select(d => new { d.CreatedAt, Amount = d.Price - d.Discount, d.Currency })
            .ToListAsync(ct);
        var byMonth = recent.GroupBy(d => MonthKey(DushanbeTime.ToLocal(d.CreatedAt)))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Sum: g.Sum(d => Tjs(d.Amount, d.Currency))));
        var chart = Enumerable.Range(0, Months).Select(i =>
        {
            var month = firstMonth.AddMonths(i);
            var key = $"{month.Year:D4}-{month.Month:D2}";
            var row = byMonth.GetValueOrDefault(key);
            return new DealsByMonthRow(key, row.Count, row.Sum);
        }).ToList();

        return new PlatformDashboardResponse(
            organizations,
            active,
            await db.Buildings.CountAsync(ct),
            await deals.CountAsync(ct),
            await db.Payments.SumAsync(p => (decimal?)p.AmountTjs, ct) ?? 0,
            await db.Customers.CountAsync(ct),
            await db.Users.CountAsync(u => u.OrganizationId != null, ct),
            revenueRows.Sum(r => Tjs(r.Price, r.Currency)),
            await db.Organizations.CountAsync(o => o.CreatedAt >= monthStart, ct),
            await deals.CountAsync(d => d.CreatedAt >= monthStart, ct),
            top.Select(o => new TopOrganizationRow(o.Id, o.Name, o.Deals, o.Payments)).ToList(),
            chart);
    }

    // Полночь 1-го числа по Душанбе в UTC.
    private static DateTime MonthStartUtc(int year, int month) =>
        DateTime.SpecifyKind(new DateTime(year, month, 1) - DushanbeTime.Offset, DateTimeKind.Utc);

    private static string MonthKey(DateTime local) => $"{local.Year:D4}-{local.Month:D2}";
}

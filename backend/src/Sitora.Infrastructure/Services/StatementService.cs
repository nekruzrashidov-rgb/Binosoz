using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Акт сверки на дату: всё считается по графику и платежам не позже этой даты.
public class StatementService(AppDbContext db, IPdfGenerator pdf, TimeProvider timeProvider)
{
    // Шапка документов по сделке (акт сверки, график).
    public Task<DealHeader> GetDealHeaderAsync(Guid dealId, CancellationToken ct) =>
        db.Deals.AsNoTracking()
            .Where(d => d.Id == dealId)
            .Select(d => new DealHeader(d.Id, d.Apartment.Floor.Building.NameRu, d.Apartment.Floor.Number,
                d.Apartment.Number, d.Customer.FullName))
            .SingleAsync(ct);

    public async Task<StatementData> BuildAsync(Guid dealId, DateOnly asOf, CancellationToken ct)
    {
        var deal = await GetDealHeaderAsync(dealId, ct);
        var currency = await db.Deals.Where(d => d.Id == dealId).Select(d => d.Currency).SingleAsync(ct);

        var plan = await db.PaymentPlans.AsNoTracking().Include(p => p.Items)
                       .FirstOrDefaultAsync(p => p.DealId == dealId, ct)
                   ?? throw new BusinessRuleException("График платежей ещё не составлен");

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.DealId == dealId && p.PaidAt <= asOf)
            .OrderBy(p => p.PaidAt).ThenBy(p => p.CreatedAt)
            .Select(p => new StatementPayment(p.PaidAt, p.Amount, p.Currency, p.RateUsed, p.AmountInDealCurrency, p.ReversalOfId != null))
            .ToListAsync(ct);

        var accrued = plan.Items.Where(i => i.DueDate <= asOf).Sum(i => i.AmountDue);
        var paid = payments.Sum(p => p.AmountInDealCurrency);

        return new StatementData(
            asOf,
            timeProvider.GetUtcNow().UtcDateTime,
            deal.BuildingName,
            deal.FloorNumber,
            deal.ApartmentNumber,
            deal.CustomerName,
            deal.DealId,
            currency,
            plan.Total,
            accrued,
            paid,
            Math.Max(accrued - paid, 0),
            plan.Total - paid,
            payments);
    }

    public byte[] Render(StatementData data) => pdf.RenderStatement(data);

    public async Task<byte[]> RenderScheduleAsync(Guid dealId, ScheduleResponse schedule, DateOnly asOf, CancellationToken ct)
    {
        var header = await GetDealHeaderAsync(dealId, ct);
        return pdf.RenderSchedule(new ScheduleDocumentData(header, schedule, asOf, timeProvider.GetUtcNow().UtcDateTime));
    }
}

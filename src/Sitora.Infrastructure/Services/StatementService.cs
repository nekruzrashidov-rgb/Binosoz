using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Акт сверки на дату: всё считается по графику и платежам не позже этой даты.
public class StatementService(AppDbContext db, IPdfGenerator pdf, TimeProvider timeProvider)
{
    public async Task<StatementData> BuildAsync(Guid dealId, DateOnly asOf, CancellationToken ct)
    {
        var deal = await db.Deals.AsNoTracking()
            .Where(d => d.Id == dealId)
            .Select(d => new
            {
                d.Id, d.Currency, d.Customer.FullName,
                Building = d.Apartment.Floor.Building.NameRu,
                Floor = d.Apartment.Floor.Number,
                Apartment = d.Apartment.Number
            })
            .SingleAsync(ct);

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
            deal.Building,
            deal.Floor,
            deal.Apartment,
            deal.FullName,
            deal.Id,
            deal.Currency,
            plan.Total,
            accrued,
            paid,
            Math.Max(accrued - paid, 0),
            plan.Total - paid,
            payments);
    }

    public byte[] Render(StatementData data) => pdf.RenderStatement(data);
}

using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class CrmService(AppDbContext db, TimeProvider timeProvider) : ICrmService
{
    private const int CardsPerColumn = 50;

    private static readonly DealStage[] PipelineStages =
        [DealStage.Lead, DealStage.Showing, DealStage.Booking, DealStage.Contract, DealStage.Payment, DealStage.Completed];

    private static readonly DealStage[] SoldStages = [DealStage.Contract, DealStage.Payment, DealStage.Completed];

    public async Task<CrmSummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var soon = now.AddHours(24);

        var newLeads = await db.Leads.CountAsync(l => l.Status == LeadStatus.New, ct);
        var leadsInProgress = await db.Leads.CountAsync(l => l.Status == LeadStatus.InProgress, ct);
        var activeBookings = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Active, ct);
        var expiringSoon = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Active && b.ExpiresAt <= soon, ct);

        var dealsByStage = await db.Deals.GroupBy(d => d.Stage)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var apartmentsByStatus = await db.Apartments.GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var sold = await db.Deals.Where(d => SoldStages.Contains(d.Stage))
            .GroupBy(d => d.Currency)
            .Select(g => new MoneyTotal(g.Key, g.Sum(d => d.Price - d.Discount)))
            .ToListAsync(ct);

        return new CrmSummaryResponse(
            newLeads,
            leadsInProgress,
            activeBookings,
            expiringSoon,
            Enum.GetValues<DealStage>().ToDictionary(s => s, s => dealsByStage.GetValueOrDefault(s)),
            Enum.GetValues<ApartmentStatus>().ToDictionary(s => s, s => apartmentsByStatus.GetValueOrDefault(s)),
            sold);
    }

    public async Task<PipelineResponse> GetPipelineAsync(CancellationToken ct = default)
    {
        var columns = new List<PipelineColumn>();
        foreach (var stage in PipelineStages)
        {
            var dealsQuery = db.Deals.AsNoTracking().Where(d => d.Stage == stage);
            var count = await dealsQuery.CountAsync(ct);
            var cards = await dealsQuery.OrderByDescending(d => d.UpdatedAt).Take(CardsPerColumn)
                .Select(d => new PipelineCard(PipelineCardKind.Deal, d.Id, d.Customer.FullName, d.Customer.Phone,
                    d.ApartmentId, d.Apartment.Number, d.Price - d.Discount, d.Currency, d.CreatedAt, null))
                .ToListAsync(ct);

            // Заявки и брони без сделки показываем в колонках «Заявка» и «Бронь», чтобы менеджер видел их сразу.
            if (stage == DealStage.Lead)
            {
                var leads = OpenLeadsWithoutBooking();
                count += await leads.CountAsync(ct);
                cards.AddRange(await leads.OrderByDescending(l => l.CreatedAt).Take(CardsPerColumn)
                    .Select(l => new PipelineCard(PipelineCardKind.Lead, l.Id, l.Name, l.Phone,
                        l.ApartmentId, l.Apartment != null ? l.Apartment.Number : null, null, null, l.CreatedAt, null))
                    .ToListAsync(ct));
            }
            else if (stage == DealStage.Booking)
            {
                var bookings = db.Bookings.AsNoTracking().Where(b => b.Status == BookingStatus.Active);
                count += await bookings.CountAsync(ct);
                cards.AddRange(await bookings.OrderByDescending(b => b.CreatedAt).Take(CardsPerColumn)
                    .Select(b => new PipelineCard(PipelineCardKind.Booking, b.Id,
                        b.Customer != null ? b.Customer.FullName : b.Lead!.Name,
                        b.Customer != null ? b.Customer.Phone : b.Lead!.Phone,
                        b.ApartmentId, b.Apartment.Number, b.Apartment.Price, b.Apartment.Currency, b.CreatedAt, b.ExpiresAt))
                    .ToListAsync(ct));
            }

            columns.Add(new PipelineColumn(stage, count,
                cards.OrderByDescending(c => c.CreatedAt).Take(CardsPerColumn).ToList()));
        }

        return new PipelineResponse(columns);
    }

    private IQueryable<Domain.Entities.Lead> OpenLeadsWithoutBooking() =>
        db.Leads.AsNoTracking().Where(l =>
            (l.Status == LeadStatus.New || l.Status == LeadStatus.InProgress)
            && !db.Bookings.Any(b => b.LeadId == l.Id && b.Status == BookingStatus.Active));
}

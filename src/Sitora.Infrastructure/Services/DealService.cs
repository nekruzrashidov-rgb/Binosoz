using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class DealService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    IExchangeRateService rates,
    TimeProvider timeProvider,
    CatalogCache cache) : IDealService
{
    private const string DealNotFound = "Сделка не найдена";
    private const string OpenDealExists = "По этой квартире уже есть открытая сделка";

    public async Task<PagedResult<DealResponse>> GetAsync(DealStage? stage, Guid? customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        var query = db.Deals.AsNoTracking();
        if (stage is not null)
            query = query.Where(d => d.Stage == stage);
        if (customerId is not null)
            query = query.Where(d => d.CustomerId == customerId);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(d => d.UpdatedAt)
            .Skip(p.Skip).Take(p.PageSize)
            .Select(ToResponse)
            .ToListAsync(ct);
        return new PagedResult<DealResponse>(items, p.Page, p.PageSize, total);
    }

    public async Task<DealResponse> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Deals.AsNoTracking().Where(d => d.Id == id).Select(ToResponse).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(DealNotFound);

    public async Task<IReadOnlyList<DealResponse>> GetMyDealsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Требуется вход");
        return await db.Deals.AsNoTracking()
            .Where(d => d.Customer.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(ToResponse)
            .ToListAsync(ct);
    }

    public async Task<DealResponse> CreateAsync(CreateDealRequest request, CancellationToken ct = default)
    {
        var currency = request.Currency ?? Currency.TJS;
        // Цена в каталоге в сомони; для договора в USD она пересчитывается по курсу на сегодня
        // и округляется до целого доллара.
        decimal? usdRate = null;
        if (currency == Currency.USD)
        {
            usdRate = await rates.GetRateOnAsync(Currency.USD, DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime), ct)
                ?? throw new BusinessRuleException("Нет курса USD. Бухгалтер должен ввести курс, прежде чем оформлять сделку в долларах");
        }

        var target = request.Stage ?? DealStage.Lead;
        if (target is DealStage.Completed or DealStage.Cancelled)
            throw new BusinessRuleException("Новую сделку нельзя создать сразу завершённой или отменённой");

        var managerId = currentUser.UserId ?? throw new UnauthorizedException("Требуется вход");
        Deal deal;
        ApartmentStatus statusBefore;
        Apartment apartment;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            apartment = await db.LockApartmentAsync(request.ApartmentId, ct);
            statusBefore = apartment.Status;

            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                ?? throw new NotFoundException("Клиент не найден");
            if (apartment.Status == ApartmentStatus.Sold)
                throw new ConflictException("Квартира уже продана");
            if (await db.Deals.AnyAsync(d => d.ApartmentId == apartment.Id && d.Stage != DealStage.Cancelled, ct))
                throw new ConflictException(OpenDealExists);

            deal = new Deal { ApartmentId = apartment.Id, CustomerId = customer.Id, ManagerId = managerId };
            var price = usdRate is { } rate
                ? Math.Round(apartment.Price / rate, 0, MidpointRounding.AwayFromZero)
                : apartment.Price;
            deal.SetPrice(price, currency, request.Discount ?? 0, request.DiscountReason);
            db.Deals.Add(deal);

            // Бронь клиента на эту квартиру переходит в сделку, и сделка начинается не раньше этапа «Бронь».
            if (await TakeOverActiveBookingAsync(deal, customer, apartment.Id, ct) && target < DealStage.Booking)
                target = DealStage.Booking;

            if (target != DealStage.Lead)
                deal.ChangeStage(target, null, apartment);

            audit.Record(nameof(Deal), deal.Id, "Created", null, Snapshot(deal));
            RecordApartmentStatus(apartment, statusBefore);

            await SaveAsync(ct);
            await transaction.CommitAsync(ct);
        }

        if (apartment.Status != statusBefore)
            cache.Invalidate();
        return await GetByIdAsync(deal.Id, ct);
    }

    public async Task<DealResponse> ChangeStageAsync(Guid id, ChangeDealStageRequest request, CancellationToken ct = default)
    {
        Apartment apartment;
        ApartmentStatus statusBefore;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var apartmentId = await db.Deals.Where(d => d.Id == id).Select(d => (Guid?)d.ApartmentId).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException(DealNotFound);
            apartment = await db.LockApartmentAsync(apartmentId, ct);
            statusBefore = apartment.Status;
            var deal = await db.Deals.Include(d => d.Customer).SingleAsync(d => d.Id == id, ct);

            if (Deal.HoldsApartment(request.Stage) && !Deal.HoldsApartment(deal.Stage))
                await TakeOverActiveBookingAsync(deal, deal.Customer, apartment.Id, ct);

            var before = new { deal.Stage, deal.StageReason };
            deal.ChangeStage(request.Stage, request.Reason, apartment);
            audit.Record(nameof(Deal), deal.Id, "StageChanged", before, new { deal.Stage, deal.StageReason });
            RecordApartmentStatus(apartment, statusBefore);

            await SaveAsync(ct);
            await transaction.CommitAsync(ct);
        }

        if (apartment.Status != statusBefore)
            cache.Invalidate();
        return await GetByIdAsync(id, ct);
    }

    // Если на квартире есть активная бронь этого покупателя, сделка забирает её себе.
    // Бронь другого человека не даёт сделке занять квартиру.
    private async Task<bool> TakeOverActiveBookingAsync(Deal deal, Customer customer, Guid apartmentId, CancellationToken ct)
    {
        var booking = await db.Bookings.Include(b => b.Lead)
            .FirstOrDefaultAsync(b => b.ApartmentId == apartmentId && b.Status == BookingStatus.Active, ct);
        if (booking is null)
            return false;

        var sameClient = booking.CustomerId == customer.Id
                         || (booking.CustomerId is null && booking.Lead?.Phone == customer.Phone);
        if (!sameClient)
            throw new ConflictException("Квартира забронирована другим покупателем");

        var before = new { booking.Status };
        booking.Convert();
        deal.BookingId = booking.Id;
        audit.Record(nameof(Booking), booking.Id, "Converted", before, new { booking.Status, DealId = deal.Id });

        if (booking.Lead is { Status: LeadStatus.New or LeadStatus.InProgress } lead)
            lead.Status = LeadStatus.Converted;
        return true;
    }

    private void RecordApartmentStatus(Apartment apartment, ApartmentStatus before)
    {
        if (apartment.Status != before)
            audit.Record(nameof(Apartment), apartment.Id, "StatusChanged", new { Status = before }, new { apartment.Status });
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            throw new ConflictException(OpenDealExists);
        }
    }

    private static object Snapshot(Deal d) => new
    {
        d.ApartmentId, d.CustomerId, d.ManagerId, d.Stage, d.Price, d.Discount, d.DiscountReason, d.Currency, d.BookingId
    };

    private static readonly Expression<Func<Deal, DealResponse>> ToResponse = d => new DealResponse(
        d.Id,
        d.ApartmentId,
        d.Apartment.Number,
        d.Apartment.Floor.Building.NameRu,
        d.CustomerId,
        d.Customer.FullName,
        d.ManagerId,
        d.Stage,
        d.StageReason,
        d.Price,
        d.Discount,
        d.DiscountReason,
        d.Price - d.Discount,
        d.Currency,
        d.BookingId,
        d.CreatedAt,
        d.UpdatedAt);
}

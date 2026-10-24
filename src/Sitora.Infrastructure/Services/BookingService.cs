using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class BookingService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    CatalogCache cache,
    INotificationSender notifications,
    TimeProvider timeProvider,
    ILogger<BookingService> logger) : IBookingService
{
    private const string AlreadyBooked = "Квартира уже забронирована или недоступна";

    // Время в уведомлениях показываем по Душанбе (UTC+5, без перехода на летнее время).
    private static readonly TimeSpan DushanbeOffset = TimeSpan.FromHours(5);

    public async Task<BookingResponse> CreateAsync(Guid apartmentId, CreateBookingRequest request, CancellationToken ct = default)
    {
        var isStaff = currentUser.IsInRole(RoleNames.Manager) || currentUser.IsInRole(RoleNames.Admin);
        if (request.CustomerId is not null && !isStaff)
            throw new ForbiddenException("Бронировать на клиента может только менеджер");

        var now = Now;
        Booking booking;
        Apartment apartment;
        string clientName;
        string clientPhone;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            apartment = await db.LockApartmentAsync(apartmentId, ct);
            if (apartment.Status != ApartmentStatus.Free)
                throw new ConflictException(AlreadyBooked);

            booking = new Booking { ApartmentId = apartment.Id, ExpiresAt = now.Add(Booking.Duration) };

            if (request.CustomerId is { } customerId)
            {
                var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
                    ?? throw new NotFoundException("Клиент не найден");
                booking.CustomerId = customer.Id;
                (clientName, clientPhone) = (customer.FullName, customer.Phone);
            }
            else
            {
                var lead = await FindOrCreateLeadAsync(request, apartment.Id, isStaff, ct);
                booking.LeadId = lead.Id;
                (clientName, clientPhone) = (lead.Name, lead.Phone);
            }

            apartment.Book();
            db.Bookings.Add(booking);
            audit.Record(nameof(Booking), booking.Id, "Created", null, Snapshot(booking));
            audit.Record(nameof(Apartment), apartment.Id, "StatusChanged",
                new { Status = ApartmentStatus.Free }, new { apartment.Status });

            await SaveAsync(ct);
            await transaction.CommitAsync(ct);
        }

        cache.Invalidate();
        await NotifyAsync($"Новая бронь{(isStaff ? "" : " с сайта")}: {clientName}, {clientPhone}, кв. №{apartment.Number}, " +
                          $"до {booking.ExpiresAt.Add(DushanbeOffset):dd.MM.yyyy HH:mm} (Душанбе)", ct);

        return ToResponse(booking, apartment.Number);
    }

    public async Task<BookingResponse> ExtendAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (booking, apartment) = await LoadForUpdateAsync(id, ct);

        var before = Snapshot(booking);
        booking.Extend(Now);
        audit.Record(nameof(Booking), booking.Id, "Extended", before, Snapshot(booking));

        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(booking, apartment.Number);
    }

    public async Task<BookingResponse> CancelAsync(Guid id, string reason, CancellationToken ct = default)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var (booking, apartment) = await LoadForUpdateAsync(id, ct);

            var before = Snapshot(booking);
            booking.Cancel(reason.Trim());
            apartment.ReleaseBooking();
            audit.Record(nameof(Booking), booking.Id, "Cancelled", before, Snapshot(booking));
            audit.Record(nameof(Apartment), apartment.Id, "StatusChanged",
                new { Status = ApartmentStatus.Booked }, new { apartment.Status });

            await SaveAsync(ct);
            await transaction.CommitAsync(ct);

            cache.Invalidate();
            return ToResponse(booking, apartment.Number);
        }
    }

    public async Task<int> ExpireOverdueAsync(CancellationToken ct = default)
    {
        var now = Now;
        var overdue = await db.Bookings
            .Where(b => b.Status == BookingStatus.Active && b.ExpiresAt <= now)
            .Select(b => b.Id)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var id in overdue)
        {
            if (await TryExpireAsync(id, now, ct))
                expired++;
            db.ChangeTracker.Clear();
        }

        if (expired > 0)
        {
            cache.Invalidate();
            logger.LogInformation("Снято просроченных броней: {Count}", expired);
        }

        return expired;
    }

    private async Task<bool> TryExpireAsync(Guid id, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (booking, apartment) = await LoadForUpdateAsync(id, ct);

        // Пока ждали блокировку, бронь могли продлить, отменить или превратить в сделку.
        if (booking.Status != BookingStatus.Active || booking.ExpiresAt > now)
            return false;

        var before = Snapshot(booking);
        booking.Expire();
        apartment.ReleaseBooking();
        audit.Record(nameof(Booking), booking.Id, "Expired", before, Snapshot(booking));
        audit.Record(nameof(Apartment), apartment.Id, "StatusChanged",
            new { Status = ApartmentStatus.Booked }, new { apartment.Status });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private async Task<Lead> FindOrCreateLeadAsync(CreateBookingRequest request, Guid apartmentId, bool isStaff, CancellationToken ct)
    {
        var phone = PhoneNumber.Normalize(request.Phone)
            ?? throw new BusinessRuleException("Неверный номер телефона");

        // Защита от того, чтобы один посетитель сайта занял бронями много квартир.
        if (!isStaff && await db.Bookings.AnyAsync(b => b.Status == BookingStatus.Active && b.Lead!.Phone == phone, ct))
            throw new ConflictException("На этот номер уже есть активная бронь. Чтобы забронировать другую квартиру, свяжитесь с менеджером");

        var lead = await db.Leads.FirstOrDefaultAsync(
            l => l.Phone == phone && (l.Status == LeadStatus.New || l.Status == LeadStatus.InProgress), ct);
        if (lead is null)
        {
            lead = new Lead
            {
                Name = request.Name!.Trim(),
                Phone = phone,
                Source = isStaff ? LeadSource.Office : LeadSource.Website,
                Status = LeadStatus.New
            };
            db.Leads.Add(lead);
        }

        lead.ApartmentId = apartmentId;
        if (!string.IsNullOrWhiteSpace(request.Comment))
            lead.Comment = request.Comment.Trim();
        return lead;
    }

    private async Task<(Booking Booking, Apartment Apartment)> LoadForUpdateAsync(Guid id, CancellationToken ct)
    {
        var apartmentId = await db.Bookings.Where(b => b.Id == id).Select(b => (Guid?)b.ApartmentId).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Бронь не найдена");

        var apartment = await db.LockApartmentAsync(apartmentId, ct);
        // Читаем бронь уже под блокировкой квартиры, чтобы видеть последнее состояние.
        var booking = await db.Bookings.SingleAsync(b => b.Id == id, ct);
        return (booking, apartment);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            throw new ConflictException(AlreadyBooked);
        }
    }

    private async Task NotifyAsync(string text, CancellationToken ct)
    {
        try
        {
            await notifications.SendAsync(null, text, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось отправить уведомление о брони");
        }
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private static object Snapshot(Booking b) => new { b.ApartmentId, b.CustomerId, b.LeadId, b.ExpiresAt, b.Status, b.CancelReason };

    private static BookingResponse ToResponse(Booking b, int apartmentNumber) =>
        new(b.Id, b.ApartmentId, apartmentNumber, b.CustomerId, b.LeadId, b.ExpiresAt, b.Status, b.CreatedAt);
}

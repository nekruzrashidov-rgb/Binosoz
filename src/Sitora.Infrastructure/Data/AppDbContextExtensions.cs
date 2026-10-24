using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;

namespace Sitora.Infrastructure.Data;

public static class AppDbContextExtensions
{
    // Блокирует строку квартиры до конца транзакции: параллельные брони и сделки
    // по одной квартире выполняются по очереди и видят уже изменённый статус.
    public static async Task<Apartment> LockApartmentAsync(this AppDbContext db, Guid apartmentId, CancellationToken ct)
    {
        var rows = await db.Apartments
            .FromSql($"SELECT * FROM \"Apartments\" WHERE \"Id\" = {apartmentId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault() ?? throw new NotFoundException("Квартира не найдена");
    }
}

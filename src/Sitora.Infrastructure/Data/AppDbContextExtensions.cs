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

    // Платежи и сторно по одной сделке выполняются по очереди: иначе распределение по графику
    // посчиталось бы от устаревшей суммы оплат.
    public static async Task<Deal> LockDealAsync(this AppDbContext db, Guid dealId, CancellationToken ct)
    {
        var rows = await db.Deals
            .FromSql($"SELECT * FROM \"Deals\" WHERE \"Id\" = {dealId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault() ?? throw new NotFoundException("Сделка не найдена");
    }
}

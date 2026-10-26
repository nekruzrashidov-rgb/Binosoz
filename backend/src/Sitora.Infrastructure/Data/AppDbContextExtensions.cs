using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;

namespace Sitora.Infrastructure.Data;

public static class AppDbContextExtensions
{
    // Блокирует строку квартиры до конца транзакции: параллельные брони и сделки
    // по одной квартире выполняются по очереди и видят уже изменённый статус.
    // Доступ проверяется обычным запросом с фильтрами организации; сама блокировка — без фильтров,
    // чтобы FOR UPDATE оставался простым запросом к одной таблице.
    public static async Task<Apartment> LockApartmentAsync(this AppDbContext db, Guid apartmentId, CancellationToken ct)
    {
        if (!await db.Apartments.AnyAsync(a => a.Id == apartmentId, ct))
            throw new NotFoundException("Квартира не найдена");

        var rows = await db.Apartments
            .FromSql($"SELECT * FROM \"Apartments\" WHERE \"Id\" = {apartmentId} FOR UPDATE").IgnoreQueryFilters()
            .ToListAsync(ct);
        return rows.SingleOrDefault() ?? throw new NotFoundException("Квартира не найдена");
    }

    // Платежи и сторно по одной сделке выполняются по очереди: иначе распределение по графику
    // посчиталось бы от устаревшей суммы оплат.
    public static async Task<Deal> LockDealAsync(this AppDbContext db, Guid dealId, CancellationToken ct)
    {
        if (!await db.Deals.AnyAsync(d => d.Id == dealId, ct))
            throw new NotFoundException("Сделка не найдена");

        var rows = await db.Deals
            .FromSql($"SELECT * FROM \"Deals\" WHERE \"Id\" = {dealId} FOR UPDATE").IgnoreQueryFilters()
            .ToListAsync(ct);
        return rows.SingleOrDefault() ?? throw new NotFoundException("Сделка не найдена");
    }

    // Организация дома квартиры или сделки: по ней выбирается ручной курс и проверяется,
    // что клиент и квартира принадлежат одному застройщику.
    public static async Task<Guid> OrganizationOfApartmentAsync(this AppDbContext db, Guid apartmentId, CancellationToken ct) =>
        await db.Apartments.Where(a => a.Id == apartmentId)
            .Select(a => (Guid?)a.Floor.Building.OrganizationId)
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Квартира не найдена");

    public static async Task<Guid> OrganizationOfDealAsync(this AppDbContext db, Guid dealId, CancellationToken ct) =>
        await db.Deals.Where(d => d.Id == dealId)
            .Select(d => (Guid?)d.Apartment.Floor.Building.OrganizationId)
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Сделка не найдена");
}

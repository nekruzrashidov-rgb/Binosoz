using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Reviews;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Сводка кабинета. Покупатель без организации видит всё (фильтры домов к нему не применяются),
// поэтому каждая выборка ограничена его карточками клиента (Customer.UserId).
public class CabinetService(AppDbContext db, ICurrentUser currentUser) : ICabinetService
{
    public async Task<CabinetOverviewResponse> GetOverviewAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Требуется вход");
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FullName }).SingleOrDefaultAsync(ct)
            ?? throw new UnauthorizedException("Требуется вход");

        var deals = await db.Deals.AsNoTracking()
            .Where(d => d.Customer.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.Apartment.Floor.BuildingId,
                d.Apartment.Floor.Building.NameRu,
                d.Apartment.Floor.Building.NameTg,
                d.Apartment.Number,
                Floor = d.Apartment.Floor.Number,
                d.Apartment.Rooms,
                d.Apartment.Area,
                d.Stage,
                FinalPrice = d.Price - d.Discount,
                d.Currency,
                // Сторно хранится со знаком минус: сумма уже за вычетом возвратов.
                Paid = db.Payments.Where(p => p.DealId == d.Id).Sum(p => (decimal?)p.AmountInDealCurrency) ?? 0m,
                ManagerName = d.Manager.FullName,
                d.CreatedAt,
                d.Customer.OrganizationId,
            })
            .ToListAsync(ct);

        var buildingIds = deals.Select(d => d.BuildingId).Distinct().ToList();
        var reviews = await db.Reviews.AsNoTracking()
            .Where(r => r.Customer.UserId == userId && buildingIds.Contains(r.BuildingId))
            .Select(r => new ReviewResponse(r.Id, r.BuildingId, r.Rating, r.Text, r.Customer.FullName, r.OwnerReply, r.CreatedAt, r.RepliedAt, r.IsPublished))
            .ToListAsync(ct);

        // Контакты — организации последней сделки, иначе — карточки клиента (зарегистрировался, но ещё не купил).
        var organizationId = deals.Select(d => (Guid?)d.OrganizationId).FirstOrDefault()
            ?? await db.Customers.AsNoTracking().Where(c => c.UserId == userId).Select(c => (Guid?)c.OrganizationId).FirstOrDefaultAsync(ct);
        var contacts = organizationId is null
            ? new CabinetContactsResponse(null, null, null, null, null)
            : await db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
                .Select(o => new CabinetContactsResponse(o.Name, o.Phone, o.Email, o.Telegram, o.Address))
                .SingleOrDefaultAsync(ct) ?? new CabinetContactsResponse(null, null, null, null, null);

        // Название дома — на языке запроса (Accept-Language), как в каталоге.
        var tg = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tg";
        var items = deals.Select(d =>
        {
            var remaining = Math.Max(0m, d.FinalPrice - d.Paid);
            var percent = d.FinalPrice > 0 ? (int)Math.Clamp(Math.Floor(d.Paid * 100m / d.FinalPrice), 0m, 100m) : 0;
            return new CabinetDealResponse(d.Id, d.BuildingId, tg && !string.IsNullOrWhiteSpace(d.NameTg) ? d.NameTg : d.NameRu, d.Number, d.Floor, d.Rooms, d.Area, d.Stage, d.FinalPrice, d.Currency,
                d.Paid, remaining, percent, d.ManagerName, d.CreatedAt, reviews.FirstOrDefault(r => r.BuildingId == d.BuildingId));
        }).ToList();

        return new CabinetOverviewResponse(items.Count > 0, user.FullName, contacts, items);
    }
}

using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Data.Configurations;

namespace Sitora.Infrastructure.Services;

public class CatalogService(
    AppDbContext db,
    CatalogCache cache,
    IAuditService audit,
    ICurrentUser currentUser,
    ApartmentImporter importer) : ICatalogService
{
    private const string BuildingNotFound = "Дом не найден";
    private const string FloorNotFound = "Этаж не найден";
    private const string ApartmentNotFound = "Квартира не найдена";

    private static bool IsTajik => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tg";

    // Сотрудник видит каталог только своей организации, поэтому ключ кэша учитывает, кто смотрит:
    // иначе урезанный каталог одного застройщика попал бы гостям или другому застройщику.
    private Task<T> Cached<T>(string key, Func<Task<T>> factory)
    {
        var scope = currentUser.OrganizationId is { } org
            ? $"{org}:{string.Join(',', currentUser.BuildingIds ?? [])}"
            : "all";
        return cache.GetOrCreateAsync($"{scope}:{key}", factory);
    }

    // organization — slug застройщика для его публичного сайта; без него — все дома платформы.
    public Task<PagedResult<BuildingResponse>> GetBuildingsAsync(int page, int pageSize, string? organization = null, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        var tg = IsTajik;
        var slug = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim().ToLowerInvariant();
        return Cached($"buildings:{slug}:{tg}:{p.Page}:{p.PageSize}", async () =>
        {
            // Дома отключённой организации на сайте не показываются.
            var query = db.Buildings.AsNoTracking().Where(b => b.Organization.IsActive);
            if (slug is not null)
                query = query.Where(b => b.Organization.Slug == slug);
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(b => b.NameRu).ThenBy(b => b.Id)
                .Skip(p.Skip).Take(p.PageSize)
                .Select(ToBuildingResponse(tg))
                .ToListAsync(ct);
            return new PagedResult<BuildingResponse>(items, p.Page, p.PageSize, total);
        });
    }

    public async Task<Guid?> FindOrganizationIdAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return await db.Organizations.AsNoTracking()
            .Where(o => o.Slug == normalized && o.IsActive)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<BuildingResponse> GetBuildingAsync(Guid id, CancellationToken ct = default)
    {
        var tg = IsTajik;
        return Cached($"building:{id}:{tg}", async () =>
            await db.Buildings.AsNoTracking().Where(b => b.Id == id).Select(ToBuildingResponse(tg)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(BuildingNotFound));
    }

    public Task<PagedResult<FloorResponse>> GetFloorsAsync(Guid buildingId, int page, int pageSize, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        return Cached($"floors:{buildingId}:{p.Page}:{p.PageSize}", async () =>
        {
            if (!await db.Buildings.AnyAsync(b => b.Id == buildingId, ct))
                throw new NotFoundException(BuildingNotFound);

            var query = db.Floors.AsNoTracking().Where(f => f.BuildingId == buildingId);
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(f => f.Number)
                .Skip(p.Skip).Take(p.PageSize)
                .Select(ToFloorResponse)
                .ToListAsync(ct);
            return new PagedResult<FloorResponse>(items, p.Page, p.PageSize, total);
        });
    }

    public Task<PagedResult<ApartmentResponse>> GetApartmentsAsync(Guid floorId, int page, int pageSize, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        return Cached($"apartments:{floorId}:{p.Page}:{p.PageSize}", async () =>
        {
            if (!await db.Floors.AnyAsync(f => f.Id == floorId, ct))
                throw new NotFoundException(FloorNotFound);

            var query = db.Apartments.AsNoTracking().Where(a => a.FloorId == floorId);
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(a => a.Number)
                .Skip(p.Skip).Take(p.PageSize)
                .Select(ToApartmentResponse)
                .ToListAsync(ct);
            return new PagedResult<ApartmentResponse>(items, p.Page, p.PageSize, total);
        });
    }

    public Task<ApartmentResponse> GetApartmentAsync(Guid id, CancellationToken ct = default) =>
        Cached($"apartment:{id}", async () =>
            await db.Apartments.AsNoTracking().Where(a => a.Id == id).Select(ToApartmentResponse).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(ApartmentNotFound));

    // Дом создаёт владелец в своей организации (создатель платформы — войдя как владелец).
    public async Task<BuildingResponse> CreateBuildingAsync(SaveBuildingRequest request, CancellationToken ct = default)
    {
        var building = new Building
        {
            OrganizationId = currentUser.OrganizationId
                ?? throw new BusinessRuleException("Выберите организацию: войдите как её владелец")
        };
        Apply(building, request);
        db.Buildings.Add(building);
        audit.Record(nameof(Building), building.Id, "Created", null, Snapshot(building));
        await SaveAsync("Такой дом уже есть", ct);
        return await GetBuildingAsync(building.Id, ct);
    }

    public async Task<BuildingResponse> UpdateBuildingAsync(Guid id, SaveBuildingRequest request, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(BuildingNotFound);

        var maxFloor = await db.Floors.Where(f => f.BuildingId == id).MaxAsync(f => (int?)f.Number, ct) ?? 0;
        if (request.FloorsCount < maxFloor)
            throw new BusinessRuleException($"В доме уже есть этаж {maxFloor}, этажность не может быть меньше");

        var before = Snapshot(building);
        Apply(building, request);
        audit.Record(nameof(Building), building.Id, "Updated", before, Snapshot(building));
        await SaveAsync("Такой дом уже есть", ct);
        return await GetBuildingAsync(id, ct);
    }

    public async Task<FloorResponse> CreateFloorAsync(CreateFloorRequest request, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == request.BuildingId, ct)
            ?? throw new NotFoundException(BuildingNotFound);
        EnsureFloorNumber(building, request.Number);

        var floor = new Floor { BuildingId = building.Id, Number = request.Number, PlanImageUrl = request.PlanImageUrl };
        db.Floors.Add(floor);
        await SaveAsync($"Этаж {request.Number} уже есть в этом доме", ct);
        return await GetFloorAsync(floor.Id, ct);
    }

    public async Task<FloorResponse> UpdateFloorAsync(Guid id, UpdateFloorRequest request, CancellationToken ct = default)
    {
        var floor = await db.Floors.Include(f => f.Building).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException(FloorNotFound);
        EnsureFloorNumber(floor.Building, request.Number);

        floor.Number = request.Number;
        floor.PlanImageUrl = request.PlanImageUrl;
        await SaveAsync($"Этаж {request.Number} уже есть в этом доме", ct);
        return await GetFloorAsync(id, ct);
    }

    public async Task<ApartmentResponse> CreateApartmentAsync(CreateApartmentRequest request, CancellationToken ct = default)
    {
        if (!await db.Floors.AnyAsync(f => f.Id == request.FloorId, ct))
            throw new NotFoundException(FloorNotFound);

        var apartment = new Apartment
        {
            FloorId = request.FloorId,
            Number = request.Number,
            Rooms = request.Rooms,
            PlanSlot = request.PlanSlot,
            Model3dId = EmptyToNull(request.Model3dId)
        };
        apartment.SetPricing(request.Area, request.PricePerM2);
        db.Apartments.Add(apartment);
        audit.Record(nameof(Apartment), apartment.Id, "Created", null, Snapshot(apartment));
        await SaveApartmentAsync(request.Number, request.PlanSlot, ct);
        return await GetApartmentAsync(apartment.Id, ct);
    }

    public async Task<ApartmentResponse> UpdateApartmentAsync(Guid id, UpdateApartmentRequest request, CancellationToken ct = default)
    {
        var apartment = await FindApartmentAsync(id, ct);

        var before = Snapshot(apartment);
        apartment.Number = request.Number;
        apartment.Rooms = request.Rooms;
        apartment.PlanSlot = request.PlanSlot;
        apartment.Model3dId = EmptyToNull(request.Model3dId);
        apartment.SetPricing(request.Area, request.PricePerM2);
        audit.Record(nameof(Apartment), apartment.Id, "Updated", before, Snapshot(apartment));
        await SaveApartmentAsync(request.Number, request.PlanSlot, ct);
        return await GetApartmentAsync(id, ct);
    }

    public async Task<ApartmentResponse> SetOnRequestAsync(Guid id, bool onRequest, CancellationToken ct = default)
    {
        var apartment = await FindApartmentAsync(id, ct);

        var before = new { apartment.Status };
        apartment.SetOnRequest(onRequest);
        audit.Record(nameof(Apartment), apartment.Id, "StatusChanged", before, new { apartment.Status });
        await SaveAsync("Квартира изменена другим пользователем", ct);
        return await GetApartmentAsync(id, ct);
    }

    public async Task<ImportResult> ImportApartmentsAsync(Guid buildingId, Stream excelFile, CancellationToken ct = default)
    {
        var result = await importer.ImportAsync(buildingId, excelFile, ct);
        if (result.Succeeded)
            cache.Invalidate();
        return result;
    }

    private Task<FloorResponse> GetFloorAsync(Guid id, CancellationToken ct) =>
        db.Floors.AsNoTracking().Where(f => f.Id == id).Select(ToFloorResponse).FirstAsync(ct);

    private async Task<Apartment> FindApartmentAsync(Guid id, CancellationToken ct) =>
        await db.Apartments.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException(ApartmentNotFound);

    private async Task SaveAsync(string conflictMessage, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            throw new ConflictException(conflictMessage);
        }

        cache.Invalidate();
    }

    // У квартиры два уникальных правила на этаже: номер и позиция на плане.
    private async Task SaveApartmentAsync(int number, int planSlot, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            throw new ConflictException(e.ViolatedConstraint() == ApartmentConfiguration.PlanSlotIndex
                ? $"Позиция {planSlot} на плане этого этажа уже занята другой квартирой"
                : $"Квартира №{number} уже есть на этом этаже");
        }

        cache.Invalidate();
    }

    private static void EnsureFloorNumber(Building building, int number)
    {
        if (number < Floor.CommercialFloorNumber || number > building.FloorsCount)
            throw new BusinessRuleException($"Номер этажа должен быть от 0 до {building.FloorsCount}");
    }

    private static void Apply(Building building, SaveBuildingRequest request)
    {
        building.NameRu = request.NameRu.Trim();
        building.NameTg = request.NameTg.Trim();
        building.Address = request.Address.Trim();
        building.FloorsCount = request.FloorsCount;
        building.DescriptionRu = EmptyToNull(request.DescriptionRu);
        building.DescriptionTg = EmptyToNull(request.DescriptionTg);
        building.ModelUrl = request.ModelUrl?.Trim();
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(Building b) =>
        new { b.NameRu, b.NameTg, b.Address, b.FloorsCount, b.DescriptionRu, b.DescriptionTg, b.ModelUrl };

    private static object Snapshot(Apartment a) =>
        new { a.Number, a.Rooms, a.Area, a.PricePerM2, a.Price, a.Currency, a.Status, a.PlanSlot, a.Model3dId };

    private static Expression<Func<Building, BuildingResponse>> ToBuildingResponse(bool tg) => b => new BuildingResponse(
        b.Id,
        tg ? b.NameTg : b.NameRu,
        b.NameRu,
        b.NameTg,
        b.Address,
        b.FloorsCount,
        tg ? b.DescriptionTg ?? b.DescriptionRu : b.DescriptionRu,
        b.DescriptionRu,
        b.DescriptionTg,
        b.ModelUrl,
        b.Floors.SelectMany(f => f.Apartments).Count(),
        b.Floors.SelectMany(f => f.Apartments).Count(a => a.Status == ApartmentStatus.Free));

    private static readonly Expression<Func<Floor, FloorResponse>> ToFloorResponse = f => new FloorResponse(
        f.Id,
        f.BuildingId,
        f.Number,
        f.Number == Floor.CommercialFloorNumber,
        f.PlanImageUrl,
        f.Apartments.Count(),
        f.Apartments.Count(a => a.Status == ApartmentStatus.Free));

    private static readonly Expression<Func<Apartment, ApartmentResponse>> ToApartmentResponse = a => new ApartmentResponse(
        a.Id,
        a.FloorId,
        a.Floor.BuildingId,
        a.Floor.Number,
        a.Number,
        a.Rooms,
        a.Area,
        a.PricePerM2,
        a.Price,
        a.Currency,
        a.Status,
        a.PlanSlot,
        a.Model3dId);
}

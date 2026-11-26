using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Settings;
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
    ApartmentImporter importer,
    ITariffService tariffs,
    TimeProvider timeProvider,
    IConfiguration configuration) : ICatalogService
{
    // Наименьший разрыв между блоками ЖК на участке, м (Catalog__MinBlockGapMeters; по умолчанию 20).
    public const double DefaultMinBlockGap = 20;
    private double MinBlockGap => configuration.GetValue("Catalog:MinBlockGapMeters", DefaultMinBlockGap);
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
            // Гость и покупатель видят только дома на сайте; сотрудники — все дома своей организации (скрытые с пометкой).
            if (PublicOnly)
                query = query.Where(b => b.IsPublished);
            if (slug is not null)
                query = query.Where(b => b.Organization.Slug == slug);
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(b => b.SortOrder).ThenBy(b => b.NameRu).ThenBy(b => b.Id)
                .Skip(p.Skip).Take(p.PageSize)
                .Select(ToBuildingResponse(tg))
                .ToListAsync(ct);
            return new PagedResult<BuildingResponse>(await WithExpiredReleasedAsync(db, timeProvider, items, ct), p.Page, p.PageSize, total);
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
            (await WithExpiredReleasedAsync(db, timeProvider, [
                await db.Buildings.AsNoTracking().Where(b => b.Id == id).Select(ToBuildingResponse(tg)).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException(BuildingNotFound)], ct))[0]);
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
            var floorIds = items.Select(f => f.Id).ToList();
            var stale = await ExpiredBookedAsync(db, timeProvider, db.Apartments.Where(a => floorIds.Contains(a.FloorId)), ct);
            items = items.Select(f => stale.Count(x => x.FloorId == f.Id) is var n and > 0
                ? f with { FreeApartments = f.FreeApartments + n } : f).ToList();
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
            return new PagedResult<ApartmentResponse>(await ReleaseExpiredAsync(items, ct), p.Page, p.PageSize, total);
        });
    }

    public Task<ApartmentResponse> GetApartmentAsync(Guid id, CancellationToken ct = default) =>
        Cached($"apartment:{id}", async () =>
            (await ReleaseExpiredAsync([
                await db.Apartments.AsNoTracking().Where(a => a.Id == id).Select(ToApartmentResponse).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException(ApartmentNotFound)], ct))[0]);

    // Бронь истекла, а фоновая задача (раз в 5 минут; на Render сервис засыпает, и задача не идёт) её ещё не сняла:
    // для каталога квартира уже свободна. Брони читаются без фильтров организации: квартиры уже отобраны запросом
    // с фильтрами, а гость брони видеть не может — иначе истёкшие брони для него не нашлись бы.
    // Счётчики свободных квартир у этажей и домов считаются так же, чтобы не расходиться со списком квартир.
    private async Task<List<ApartmentResponse>> ReleaseExpiredAsync(List<ApartmentResponse> items, CancellationToken ct)
    {
        var booked = items.Where(a => a.Status == ApartmentStatus.Booked).Select(a => a.Id).ToList();
        if (booked.Count == 0)
            return items;
        var expired = await ExpiredAsync(db, timeProvider, booked, ct);
        return items.Select(a => expired.Contains(a.Id) ? a with { Status = ApartmentStatus.Free } : a).ToList();
    }

    // Квартиры из scope в статусе Booked с истёкшей, но ещё не снятой бронью: этаж и дом каждой.
    private static async Task<List<(Guid FloorId, Guid BuildingId)>> ExpiredBookedAsync(
        AppDbContext db, TimeProvider timeProvider, IQueryable<Apartment> scope, CancellationToken ct)
    {
        var booked = await scope.AsNoTracking().Where(a => a.Status == ApartmentStatus.Booked)
            .Select(a => new { a.Id, a.FloorId, a.Floor.BuildingId })
            .ToListAsync(ct);
        if (booked.Count == 0)
            return [];
        var expired = await ExpiredAsync(db, timeProvider, booked.Select(a => a.Id).ToList(), ct);
        return booked.Where(a => expired.Contains(a.Id)).Select(a => (a.FloorId, a.BuildingId)).ToList();
    }

    // Booked бывает и без брони (сделку вернули на этап «Бронь») — такие квартиры не трогаются: свободной
    // считается только квартира, у которой действующая по статусу бронь уже истекла.
    private static async Task<HashSet<Guid>> ExpiredAsync(AppDbContext db, TimeProvider timeProvider, List<Guid> apartmentIds, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return (await db.Bookings.IgnoreQueryFilters().AsNoTracking()
            .Where(b => apartmentIds.Contains(b.ApartmentId) && b.Status == BookingStatus.Active && b.ExpiresAt <= now)
            .Select(b => b.ApartmentId)
            .ToListAsync(ct)).ToHashSet();
    }

    // Для списков домов (каталог и публичная страница бренда): FreeApartments с учётом истёкших броней.
    internal static async Task<List<BuildingResponse>> WithExpiredReleasedAsync(
        AppDbContext db, TimeProvider timeProvider, List<BuildingResponse> items, CancellationToken ct)
    {
        if (items.Count == 0)
            return items;
        // Незаполненный эко-паспорт в ответ не попадает совсем.
        // Пустой список удобств — тоже.
        items = items.Select(b => b.Eco is { IsEmpty: true } ? b with { Eco = null } : b)
            .Select(b => b.Amenities is { Count: 0 } ? b with { Amenities = null } : b).ToList();
        var buildingIds = items.Select(b => b.Id).ToList();
        var stale = await ExpiredBookedAsync(db, timeProvider, db.Apartments.Where(a => buildingIds.Contains(a.Floor.BuildingId)), ct);
        return items.Select(b => stale.Count(x => x.BuildingId == b.Id) is var n and > 0
            ? b with { FreeApartments = b.FreeApartments + n } : b).ToList();
    }

    // Дом создаёт владелец в своей организации (создатель платформы — войдя как владелец).
    public async Task<BuildingResponse> CreateBuildingAsync(SaveBuildingRequest request, CancellationToken ct = default)
    {
        var building = new Building
        {
            OrganizationId = currentUser.OrganizationId
                ?? throw new BusinessRuleException("Выберите организацию: войдите как её владелец")
        };
        await tariffs.EnsureCanAddBuildingAsync(building.OrganizationId, ct);
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

    // Эко-паспорт дома: заменяется целиком; пустые строки — «не заполнено». Аудит и сброс кэша каталога — как у дома.
    public async Task<BuildingResponse> UpdateBuildingEcoAsync(Guid id, SaveBuildingEcoRequest request, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(BuildingNotFound);

        var before = building.Eco is null ? null : EcoSnapshot(building.Eco);
        var eco = new BuildingEcoInfo
        {
            EnergyClass = request.EnergyClass,
            WallInsulationRu = Clean(request.WallInsulationRu),
            WallInsulationTg = Clean(request.WallInsulationTg),
            WindowsRu = Clean(request.WindowsRu),
            WindowsTg = Clean(request.WindowsTg),
            WallInsulationEn = Clean(request.WallInsulationEn),
            WindowsEn = Clean(request.WindowsEn),
            SolarPanelsKw = request.SolarPanelsKw,
            HasHeatMeters = request.HasHeatMeters,
            HasWaterMeters = request.HasWaterMeters,
            HasLedCommonAreas = request.HasLedCommonAreas,
            HasEvCharging = request.HasEvCharging,
            HasRainwaterReuse = request.HasRainwaterReuse,
            HasWasteSorting = request.HasWasteSorting,
            GreenAreaM2 = request.GreenAreaM2,
            HeatingType = request.HeatingType,
            EstimatedKwhPerM2Year = request.EstimatedKwhPerM2Year
        };
        building.Eco = eco.IsEmpty ? null : eco;
        audit.Record(nameof(Building), building.Id, "EcoUpdated", before, building.Eco is null ? null : EcoSnapshot(building.Eco));
        await SaveAsync("Такой дом уже есть", ct);
        return await GetBuildingAsync(id, ct);

        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    // Удобства дома: список заменяется целиком; неизвестные ключи отсекает валидатор, порядок — как в BuildingAmenities.
    public async Task<BuildingResponse> UpdateBuildingAmenitiesAsync(Guid id, SaveBuildingAmenitiesRequest request, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(BuildingNotFound);
        var before = building.Amenities.ToList();
        building.Amenities = BuildingAmenities.Normalize(request.Amenities);
        audit.Record(nameof(Building), building.Id, "AmenitiesUpdated", new { Amenities = before }, new { building.Amenities });
        await SaveAsync("Такой дом уже есть", ct);
        return await GetBuildingAsync(id, ct);
    }

    // Точка дома на карте и высота этажа: заменяются целиком, пустое — «не задано». Пару координат проверяет валидатор.
    public async Task<BuildingResponse> UpdateBuildingLocationAsync(Guid id, SaveBuildingLocationRequest request, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(BuildingNotFound);
        var before = new { building.Latitude, building.Longitude, building.FloorHeight };
        building.Latitude = request.Latitude;
        building.Longitude = request.Longitude;
        building.FloorHeight = request.FloorHeight;
        audit.Record(nameof(Building), building.Id, "LocationUpdated", before, new { building.Latitude, building.Longitude, building.FloorHeight });
        await SaveAsync("Такой дом уже есть", ct);
        return await GetBuildingAsync(id, ct);
    }

    // Блок ЖК: буква, порядок, контур, место на участке, уступы — целиком. Контур уже проверен валидатором (вершины,
    // площадь, самопересечения); здесь — то, для чего нужна база: уступ внутри контура и не выше крыши, буква не занята,
    // блоки организации на участке не пересекаются и стоят не ближе Catalog:MinBlockGapMeters. Ошибка — 400, AuditLog —
    // в том же SaveChanges, кэш каталога сбрасывается.
    public async Task<BuildingResponse> UpdateBuildingShapeAsync(Guid id, SaveBuildingShapeRequest request, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(BuildingNotFound);
        var code = string.IsNullOrWhiteSpace(request.BlockCode) ? null : request.BlockCode.Trim().ToUpperInvariant();
        var footprint = request.Footprint is { Count: > 0 } ? request.Footprint : null;
        var setbacks = footprint is null ? null : request.Setbacks?.Where(s => s.Footprint.Count > 0).OrderBy(s => s.FromFloor).ToList();
        var position = footprint is null ? null : request.SitePosition ?? new SitePlacement();

        if (code is not null && await db.Buildings.AnyAsync(b => b.OrganizationId == building.OrganizationId && b.Id != id && b.BlockCode == code, ct))
            throw new ConflictException("Блок с такой буквой уже есть");
        if (footprint is not null)
        {
            var outline = PlanGeometry.Expand(footprint);
            foreach (var s in setbacks ?? [])
            {
                if (s.FromFloor > building.FloorsCount)
                    throw new BadRequestException("Уступ начинается выше последнего этажа");
                if (!PlanGeometry.Inside(outline, PlanGeometry.Expand(s.Footprint), 0.05))
                    throw new BadRequestException("Контур уступа выходит за контур блока");
            }

            var placed = Placed(footprint, position!);
            var others = await db.Buildings.AsNoTracking()
                .Where(b => b.OrganizationId == building.OrganizationId && b.Id != id && b.Footprint != null && b.SitePosition != null)
                .Select(b => new { b.NameRu, b.Footprint, b.SitePosition })
                .ToListAsync(ct);
            foreach (var other in others)
            {
                var gap = PlanGeometry.Distance(placed, Placed(other.Footprint!, other.SitePosition!));
                if (gap <= 1e-6)
                    throw new BadRequestException("Блоки на участке пересекаются");
                if (gap < MinBlockGap - 1e-6)
                    throw new BadRequestException(string.Create(CultureInfo.InvariantCulture,
                        $"Расстояние между блоками меньше допустимого: {gap:0.#} м, нужно не меньше {MinBlockGap:0.#} м"));
            }
        }

        var before = ShapeSnapshot(building);
        building.BlockCode = code;
        building.SortOrder = request.SortOrder;
        building.Footprint = footprint;
        building.SitePosition = position;
        building.Setbacks = setbacks is { Count: > 0 } ? setbacks : null;
        audit.Record(nameof(Building), building.Id, "ShapeUpdated", before, ShapeSnapshot(building));
        await SaveAsync("Блок с такой буквой уже есть", ct);
        return await GetBuildingAsync(id, ct);

        static IReadOnlyList<PlanGeometry.P> Placed(List<double[]> fp, SitePlacement p) =>
            PlanGeometry.Place(PlanGeometry.Expand(fp), p.X, p.Y, p.RotationDeg);
    }

    // Правило цены блока целиком (руководители). Квартиры не меняются — для этого «Пересчитать цены» (ApplyPriceRuleAsync).
    public async Task<BuildingResponse> UpdatePriceRuleAsync(Guid id, SavePriceRuleRequest request, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(BuildingNotFound);
        var before = new { building.PriceCurrency, building.PriceSteps, building.PenthousePricePerM2, building.CommercialPricePerM2, building.ParkingSpotPrice };
        building.PriceCurrency = request.Currency;
        building.PriceSteps = request.Steps is { Count: > 0 } ? request.Steps.OrderBy(s => s.FromFloor).ToList() : null;
        building.PenthousePricePerM2 = request.PenthousePricePerM2;
        building.CommercialPricePerM2 = request.CommercialPricePerM2;
        building.ParkingSpotPrice = request.ParkingSpotPrice;
        audit.Record(nameof(Building), building.Id, "PriceRuleUpdated", before, new { building.PriceCurrency, building.PriceSteps, building.PenthousePricePerM2,
            building.CommercialPricePerM2, building.ParkingSpotPrice });
        await SaveAsync("Правило цены изменено другим пользователем", ct);
        return await GetBuildingAsync(id, ct);
    }

    // «Пересчитать цены»: новая цена по правилу — только свободным квартирам блока (бронь, продажа и «под запрос» не
    // трогаются). Каждое изменение цены — запись PriceChanged в AuditLog, всё одним SaveChanges (одна транзакция);
    // кэш каталога сбрасывается. Последний этаж — пентхаусы (своя цена м²).
    public async Task<ApplyPriceRuleResponse> ApplyPriceRuleAsync(Guid id, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(BuildingNotFound);
        if (building.PriceSteps is not { Count: > 0 })
            throw new BusinessRuleException("Сначала задайте цены по этажам");
        var apartments = await db.Apartments.Include(a => a.Floor).Where(a => a.Floor.BuildingId == id).ToListAsync(ct);
        int updated = 0, unchanged = 0, skipped = 0;
        foreach (var a in apartments)
        {
            var (ppm, fixedPrice) = PriceRules.PriceOf(building, a, a.Floor.Number);
            if (a.Status != ApartmentStatus.Free || (ppm is null && fixedPrice is null))
            {
                skipped++;
                continue;
            }
            if (a.Currency == building.PriceCurrency && (fixedPrice is { } fp ? a.Price == fp : a.PricePerM2 == ppm))
            {
                unchanged++;
                continue;
            }
            var before = new { a.PricePerM2, a.Price, a.Currency };
            if (fixedPrice is { } spot)
                a.SetFixedPrice(a.Area, spot, building.PriceCurrency);
            else
                a.SetPricing(a.Area, ppm!.Value, building.PriceCurrency);
            audit.Record(nameof(Apartment), a.Id, "PriceChanged", before, new { a.PricePerM2, a.Price, a.Currency, Source = "PriceRule" });
            updated++;
        }
        await SaveAsync("Квартира изменена другим пользователем", ct);
        return new ApplyPriceRuleResponse(updated, unchanged, skipped);
    }

    // Гость, покупатель и создатель платформы вне организации — публичный каталог.
    private bool PublicOnly => currentUser.OrganizationId is null || currentUser.IsInRole(RoleNames.Customer);

    // Показать дом на сайте или скрыть (руководители): квартиры, брони, сделки, графики и платежи не меняются.
    public async Task<BuildingResponse> SetPublishedAsync(Guid id, bool isPublished, CancellationToken ct = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(BuildingNotFound);
        if (building.IsPublished != isPublished)
        {
            audit.Record(nameof(Building), building.Id, isPublished ? "Published" : "Unpublished", new { building.IsPublished }, new { IsPublished = isPublished });
            building.IsPublished = isPublished;
            await SaveAsync("Дом изменён другим пользователем", ct);
        }
        return await GetBuildingAsync(id, ct);
    }

    private static object ShapeSnapshot(Building b) => new { b.BlockCode, b.SortOrder, b.Footprint, b.SitePosition, b.Setbacks };

    public async Task<SitePlan?> GetSitePlanAsync(CancellationToken ct = default)
    {
        var organizationId = currentUser.OrganizationId ?? throw new NotFoundException("Организация не найдена");
        return await db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.SitePlan).FirstOrDefaultAsync(ct);
    }

    // План участка своей организации целиком (руководители); пустой контур — плана нет. AuditLog, сброс кэша каталога.
    public async Task<SitePlan?> UpdateSitePlanAsync(SaveSitePlanRequest request, CancellationToken ct = default)
    {
        var organizationId = currentUser.OrganizationId ?? throw new NotFoundException("Организация не найдена");
        var organization = await db.Organizations.FirstAsync(o => o.Id == organizationId, ct);
        var before = organization.SitePlan;
        organization.SitePlan = request.Boundary is { Count: >= 3 }
            ? new SitePlan { Boundary = request.Boundary, Zones = request.Zones ?? [] }
            : null;
        audit.Record(nameof(Organization), organization.Id, "SitePlanUpdated", before is null ? null : new { before.Boundary, before.Zones },
            organization.SitePlan is null ? null : new { organization.SitePlan.Boundary, organization.SitePlan.Zones }, organization.Id);
        await SaveAsync("План участка изменён другим пользователем", ct);
        return organization.SitePlan;
    }

    private static object EcoSnapshot(BuildingEcoInfo e) => new
    {
        e.EnergyClass, e.WallInsulationRu, e.WallInsulationTg, e.WindowsRu, e.WindowsTg, e.WallInsulationEn, e.WindowsEn, e.SolarPanelsKw,
        e.HasHeatMeters, e.HasWaterMeters, e.HasLedCommonAreas, e.HasEvCharging, e.HasRainwaterReuse, e.HasWasteSorting,
        e.GreenAreaM2, e.HeatingType, e.EstimatedKwhPerM2Year
    };

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
            await cache.InvalidateAsync(ct);
        return result;
    }

    public byte[] BuildImportTemplate() => ApartmentImporter.BuildTemplate();

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

        await cache.InvalidateAsync(ct);
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

        await cache.InvalidateAsync(ct);
    }

    // Подземные этажи паркинга — отрицательные (−1, −2…), 0 — прежний коммерческий этаж.
    public const int MinFloorNumber = -5;

    private static void EnsureFloorNumber(Building building, int number)
    {
        if (number < MinFloorNumber || number > building.FloorsCount)
            throw new BusinessRuleException($"Номер этажа должен быть от {MinFloorNumber} до {building.FloorsCount}");
    }

    private static void Apply(Building building, SaveBuildingRequest request)
    {
        building.NameRu = request.NameRu.Trim();
        building.NameTg = request.NameTg.Trim();
        building.Address = request.Address.Trim();
        building.FloorsCount = request.FloorsCount;
        building.DescriptionRu = EmptyToNull(request.DescriptionRu);
        building.DescriptionTg = EmptyToNull(request.DescriptionTg);
        building.NameEn = EmptyToNull(request.NameEn);
        building.DescriptionEn = EmptyToNull(request.DescriptionEn);
        building.ModelUrl = request.ModelUrl?.Trim();
        building.CompletionYear = request.CompletionYear;
        building.PlannedApartments = request.PlannedApartments;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(Building b) =>
        new { b.NameRu, b.NameTg, b.NameEn, b.Address, b.FloorsCount, b.DescriptionRu, b.DescriptionTg, b.DescriptionEn, b.ModelUrl, b.CompletionYear, b.PlannedApartments };

    private static object Snapshot(Apartment a) =>
        new { a.Number, a.Rooms, a.Area, a.PricePerM2, a.Price, a.Currency, a.Status, a.PlanSlot, a.Model3dId };

    internal static Expression<Func<Building, BuildingResponse>> ToBuildingResponse(bool tg) => b => new BuildingResponse(
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
        b.Floors.SelectMany(f => f.Apartments).Count(a => a.Kind == ApartmentKind.Residential || a.Kind == ApartmentKind.Penthouse),
        b.Floors.SelectMany(f => f.Apartments).Count(a => (a.Kind == ApartmentKind.Residential || a.Kind == ApartmentKind.Penthouse) && a.Status == ApartmentStatus.Free),
        b.CompletionYear,
        b.PlannedApartments,
        b.Eco == null ? null : new BuildingEcoResponse(
            b.Eco.EnergyClass,
            tg ? b.Eco.WallInsulationTg ?? b.Eco.WallInsulationRu : b.Eco.WallInsulationRu ?? b.Eco.WallInsulationTg,
            b.Eco.WallInsulationRu,
            b.Eco.WallInsulationTg,
            tg ? b.Eco.WindowsTg ?? b.Eco.WindowsRu : b.Eco.WindowsRu ?? b.Eco.WindowsTg,
            b.Eco.WindowsRu,
            b.Eco.WindowsTg,
            b.Eco.SolarPanelsKw,
            b.Eco.HasHeatMeters,
            b.Eco.HasWaterMeters,
            b.Eco.HasLedCommonAreas,
            b.Eco.HasEvCharging,
            b.Eco.HasRainwaterReuse,
            b.Eco.HasWasteSorting,
            b.Eco.GreenAreaM2,
            b.Eco.HeatingType,
            b.Eco.EstimatedKwhPerM2Year,
            b.Eco.WallInsulationEn,
            b.Eco.WindowsEn),
        b.Organization.Green.IsDemo,
        b.Amenities,
        b.Organization.Installment.DownPaymentMinPct == null && b.Organization.Installment.MaxMonths == null && !b.Organization.Installment.NoOverpayment
            ? null
            : new InstallmentTermsResponse(b.Organization.Installment.DownPaymentMinPct, b.Organization.Installment.MaxMonths, b.Organization.Installment.NoOverpayment),
        b.Latitude,
        b.Longitude,
        b.FloorHeight,
        b.NameEn,
        b.DescriptionEn,
        b.BlockCode,
        b.SortOrder,
        b.Footprint,
        b.SitePosition,
        b.Setbacks,
        b.PriceSteps,
        b.PenthousePricePerM2,
        b.PriceCurrency,
        b.CommercialPricePerM2,
        b.ParkingSpotPrice,
        b.ElevatorsCount,
        b.Floors.SelectMany(f => f.Apartments).Count(a => a.Kind == ApartmentKind.Commercial),
        b.Floors.SelectMany(f => f.Apartments).Count(a => a.Kind == ApartmentKind.Parking),
        b.IsPublished);

    private static readonly Expression<Func<Floor, FloorResponse>> ToFloorResponse = f => new FloorResponse(
        f.Id,
        f.BuildingId,
        f.Number,
        f.Number == Floor.CommercialFloorNumber || (f.Apartments.Any() && f.Apartments.All(a => a.Kind == ApartmentKind.Commercial)),
        f.PlanImageUrl,
        f.Apartments.Count(),
        f.Apartments.Count(a => a.Status == ApartmentStatus.Free),
        f.Number < 0);

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
        a.Model3dId,
        a.Kind,
        a.TerraceArea);
}

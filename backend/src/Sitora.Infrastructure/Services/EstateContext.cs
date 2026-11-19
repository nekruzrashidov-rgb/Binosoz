using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Публичные данные ЖК для промпта AI-помощника — общий для гостя на сайте и покупателя в кабинете.
// Только то, что застройщик сам показывает на сайте: организация и её контакты, дома, свободные квартиры и цены,
// удобства, эко-паспорт, рассрочка, ход стройки, курс НБТ. Данных клиентов, сделок, броней и платежей здесь нет.
// Чего нет в базе — того нет и в тексте: модель не должна получать «типовые» удобства или контакты.
// Кэш 60 секунд на организацию: каждый вопрос гостя не должен пересчитывать каталог.
public class EstateContext(AppDbContext db, IExchangeRateService rates, TimeProvider time, IMemoryCache cache)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    public const string Header = "=== RESIDENTIAL COMPLEX CONTEXT (developer's database) ===";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string CacheKey(Guid organizationId) => "estate-context:" + organizationId;

    public Task<string> BuildAsync(Guid organizationId, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CacheKey(organizationId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return CreateAsync(organizationId, ct);
        })!;

    private async Task<string> CreateAsync(Guid organizationId, CancellationToken ct)
    {
        var org = await db.Organizations.IgnoreQueryFilters().AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => new { o.Name, o.Phone, o.Telegram, o.Email, o.Address, o.Installment })
            .SingleAsync(ct);

        var buildings = await db.Buildings.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.OrganizationId == organizationId)
            .OrderBy(b => b.NameRu).ThenBy(b => b.Id)
            .Select(CatalogService.ToBuildingResponse(false))
            .ToListAsync(ct);
        // Квартиры с истёкшей бронью сайт уже показывает свободными — так же и здесь.
        buildings = await CatalogService.WithExpiredReleasedAsync(db, time, buildings, ct);
        var buildingIds = buildings.Select(b => b.Id).ToList();

        var free = await db.Apartments.IgnoreQueryFilters().AsNoTracking()
            .Where(a => buildingIds.Contains(a.Floor.BuildingId) && a.Status == ApartmentStatus.Free
                        && a.Floor.Number != Floor.CommercialFloorNumber)
            .Select(a => new { a.Floor.BuildingId, a.Rooms, a.Area, a.Price, a.Currency })
            .ToListAsync(ct);
        var readiness = (await db.ConstructionStages.IgnoreQueryFilters().AsNoTracking()
                .Where(s => buildingIds.Contains(s.BuildingId))
                .Select(s => new { s.BuildingId, s.Percent }).ToListAsync(ct))
            .GroupBy(s => s.BuildingId).ToDictionary(g => g.Key, g => (int)Math.Round(g.Average(s => s.Percent)));
        var usd = await rates.FindCurrentAsync(Currency.USD, organizationId, ct);

        var lines = new List<string> { Header, $"Developer: {org.Name}" };

        var contacts = new List<string>();
        if (!string.IsNullOrWhiteSpace(org.Phone)) contacts.Add("phone " + org.Phone.Trim());
        if (!string.IsNullOrWhiteSpace(org.Telegram)) contacts.Add("Telegram " + org.Telegram.Trim());
        if (!string.IsNullOrWhiteSpace(org.Email)) contacts.Add("e-mail " + org.Email.Trim());
        if (!string.IsNullOrWhiteSpace(org.Address)) contacts.Add("sales office " + org.Address.Trim());
        lines.Add(contacts.Count > 0
            ? "Contacts: " + string.Join("; ", contacts) + ". Office hours are not in the database."
            : "Contacts: not in the database — point to the contact page /contact.");

        if (buildings.Count == 0)
            lines.Add("Buildings: none published yet — sales have not opened.");

        foreach (var b in buildings)
        {
            lines.Add("");
            var head = $"Building \"{b.NameRu}\"";
            if (!string.IsNullOrWhiteSpace(b.NameTg) && b.NameTg != b.NameRu) head += $" (тоҷикӣ: \"{b.NameTg}\")";
            if (!string.IsNullOrWhiteSpace(b.Address)) head += ", address: " + b.Address;
            lines.Add(head);

            var facts = new List<string> { $"{b.FloorsCount} floors" };
            if (b.FloorHeight is { } h) facts.Add($"floor-to-floor height {h.ToString("0.##", Invariant)} m");
            if (b.CompletionYear is { } year) facts.Add($"planned delivery {year}");
            if (readiness.TryGetValue(b.Id, out var ready)) facts.Add($"construction {ready}% complete");
            facts.Add($"apartments in the catalog: {b.TotalApartments}, free now: {b.FreeApartments}");
            lines.Add("- " + string.Join("; ", facts));

            var mine = free.Where(a => a.BuildingId == b.Id).ToList();
            if (mine.Count == 0)
                lines.Add("- Free apartments: none at the moment.");
            else
            {
                lines.Add("- Free apartments by number of rooms (prices are final catalog prices):");
                foreach (var g in mine.GroupBy(a => new { a.Rooms, a.Currency }).OrderBy(g => g.Key.Rooms).ThenBy(g => g.Key.Currency))
                {
                    var rooms = g.Key.Rooms <= 0 ? "studio" : $"{g.Key.Rooms}-room";
                    lines.Add($"  {rooms}: {g.Count()} free, price {Range(g.Min(a => a.Price), g.Max(a => a.Price), "N0")} {g.Key.Currency}, " +
                              $"area {Range(g.Min(a => a.Area), g.Max(a => a.Area), "0.##")} m²");
                }
            }

            if (b.Amenities is { Count: > 0 } amenities)
                lines.Add("- Amenities: " + string.Join(", ", amenities.Select(k => k.Replace('_', ' '))));
            if (b.Eco is { } eco && Eco(eco) is { Count: > 0 } ecoFacts)
                lines.Add("- Eco passport: " + string.Join("; ", ecoFacts));
        }

        lines.Add("");
        var terms = new List<string>();
        if (org.Installment.DownPaymentMinPct is { } down) terms.Add($"minimum down payment {down.ToString("0.##", Invariant)}%");
        if (org.Installment.MaxMonths is { } months) terms.Add($"up to {months} months");
        if (org.Installment.NoOverpayment) terms.Add("no overpayment (0%): the rest is split into equal monthly payments");
        lines.Add(terms.Count > 0
            ? "Installment plan: " + string.Join("; ", terms) + "."
            : "Installment plan: terms are not set in the database — ask the manager.");

        lines.Add("How to buy: choose an apartment in the catalog /apartments (3D model and floor plans) → book it online " +
                  $"(booking is free and holds the apartment for {(int)Booking.Duration.TotalHours} hours) → contract with the manager → " +
                  "payment schedule. The contract, payment schedule and documents are available online in the buyer's personal cabinet after sign-in.");
        if (usd is not null)
            lines.Add($"USD rate of the National Bank of Tajikistan on {usd.Date.ToString("dd.MM.yyyy", Invariant)}: " +
                      $"1 USD = {usd.RateToTjs.ToString("0.####", Invariant)} TJS — dollar amounts are estimates only, say \"approximately, at the NBT rate\".");
        lines.Add("Site pages: /apartments (catalog, 3D, floor plans), /about, /gallery, /contact, /privacy.");
        lines.Add(new string('=', 20));
        return string.Join("\n", lines);
    }

    private static string Range(decimal min, decimal max, string format) =>
        min == max ? min.ToString(format, Invariant) : $"from {min.ToString(format, Invariant)} to {max.ToString(format, Invariant)}";

    // Только заполненные пункты эко-паспорта.
    private static List<string> Eco(Sitora.Domain.DTOs.Catalog.BuildingEcoResponse eco)
    {
        var facts = new List<string>();
        if (eco.EnergyClass is { } cls) facts.Add("energy efficiency class " + cls switch
        {
            EnergyClass.APlusPlus => "A++",
            EnergyClass.APlus => "A+",
            _ => cls.ToString()
        });
        if (!string.IsNullOrWhiteSpace(eco.WallInsulationRu)) facts.Add("wall insulation: " + eco.WallInsulationRu);
        if (!string.IsNullOrWhiteSpace(eco.WindowsRu)) facts.Add("windows: " + eco.WindowsRu);
        if (eco.HeatingType is { } heating) facts.Add("heating: " + heating);
        if (eco.SolarPanelsKw is { } kw) facts.Add($"solar panels {kw.ToString("0.##", Invariant)} kW");
        if (eco.EstimatedKwhPerM2Year is { } kwh) facts.Add($"design energy use {kwh.ToString("0.##", Invariant)} kWh/m² per year");
        if (eco.GreenAreaM2 is { } green) facts.Add($"green area {green.ToString("0.##", Invariant)} m²");
        void Flag(bool? value, string name)
        {
            if (value is { } v) facts.Add(name + (v ? ": yes" : ": no"));
        }
        Flag(eco.HasHeatMeters, "heat meters");
        Flag(eco.HasWaterMeters, "water meters");
        Flag(eco.HasLedCommonAreas, "LED lighting in common areas");
        Flag(eco.HasEvCharging, "EV charging");
        Flag(eco.HasRainwaterReuse, "rainwater reuse");
        Flag(eco.HasWasteSorting, "waste sorting");
        return facts;
    }
}

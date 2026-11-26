using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Data.Seed;

// Второй застройщик для разработки: организация «Второй застройщик», её владелец и один дом.
// Нужен, чтобы вживую видеть разделение данных между застройщиками. Раньше он назывался «Rich Tower» (slug rich-tower);
// теперь «Rich Tower» — ЖК организации по умолчанию, и прежний второй застройщик в базах разработки переименовывается.
public static class SecondBuilderSeeder
{
    public const string Name = "Второй застройщик";
    public const string Slug = "second-builder";
    public const string OwnerEmail = "owner@second-builder.tj";
    public const string OwnerPasswordVariable = "SEED_SECONDBUILDER_OWNER_PASSWORD";
    public const string BuildingName = "Дом второго застройщика";
    private const string LegacySlug = "rich-tower", LegacyName = "Rich Tower";
    private const int Floors = 8;
    private static readonly (int Rooms, decimal Area)[] Layout = [(1, 42m), (2, 64m), (3, 88m), (2, 61m)];

    public static async Task SeedAsync(AppDbContext db, UserManager<AppUser> userManager, IAuditService audit,
        IConfiguration configuration, ILogger logger)
    {
        var organization = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Slug == Slug)
            ?? await RenameLegacyAsync(db, audit, logger);
        if (organization is null)
        {
            organization = new Organization
            {
                Name = Name,
                Slug = Slug,
                PrimaryColor = "#b8862b",
                Address = "г. Душанбе"
            };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync();
            logger.LogInformation("Создана организация {Name} ({Slug})", organization.Name, Slug);
        }

        await SeedOwnerAsync(organization, userManager, configuration, logger);

        if (await db.Buildings.IgnoreQueryFilters().AnyAsync(b => b.OrganizationId == organization.Id))
            return;

        var building = new Building
        {
            OrganizationId = organization.Id,
            NameRu = BuildingName,
            NameTg = "Бинои сохтмончии дуюм",
            Address = "г. Душанбе, ул. Айни, 10",
            FloorsCount = Floors,
            DescriptionRu = "Жилой дом второго застройщика (данные разработки)"
        };
        for (var floorNumber = 1; floorNumber <= Floors; floorNumber++)
        {
            var floor = new Floor { Number = floorNumber };
            for (var position = 1; position <= Layout.Length; position++)
            {
                var (rooms, area) = Layout[position - 1];
                var apartment = new Apartment { Number = floorNumber * 100 + position, Rooms = rooms, PlanSlot = position - 1 };
                apartment.SetPricing(area, 9800m + floorNumber * 120m);
                floor.Apartments.Add(apartment);
            }

            building.Floors.Add(floor);
        }

        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        logger.LogInformation("Создан дом {Building} застройщика {Organization}", building.NameRu, organization.Name);
    }

    // База разработки, созданная до переименования: организация rich-tower «Rich Tower» и её дом «Rich Tower» получают новые
    // имя и slug (AuditLog Renamed в том же SaveChanges). Имя, заданное вручную, не трогается.
    private static async Task<Organization?> RenameLegacyAsync(AppDbContext db, IAuditService audit, ILogger logger)
    {
        var organization = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Slug == LegacySlug && o.Name == LegacyName);
        if (organization is null)
            return null;
        audit.Record(nameof(Organization), organization.Id, CatalogSeeder.RenamedAction, new { organization.Name, organization.Slug },
            new { Name, Slug }, organization.Id);
        organization.Name = Name;
        organization.Slug = Slug;
        foreach (var building in await db.Buildings.IgnoreQueryFilters()
                     .Where(b => b.OrganizationId == organization.Id && b.NameRu == LegacyName).ToListAsync())
        {
            audit.Record(nameof(Building), building.Id, CatalogSeeder.RenamedAction, new { building.NameRu }, new { NameRu = BuildingName },
                organization.Id);
            building.NameRu = BuildingName;
            building.NameTg = "Бинои сохтмончии дуюм";
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Второй застройщик «{Old}» переименован в «{New}» ({Slug})", LegacyName, Name, Slug);
        return organization;
    }

    // Пароль — из настройки SEED_SECONDBUILDER_OWNER_PASSWORD (user secrets или окружение), в репозитории его нет.
    // Без пароля владелец входит по коду из почты.
    private static async Task SeedOwnerAsync(Organization organization, UserManager<AppUser> userManager, IConfiguration configuration,
        ILogger logger)
    {
        if (await userManager.FindByEmailAsync(OwnerEmail) is not null)
            return;

        var owner = new AppUser
        {
            UserName = OwnerEmail,
            Email = OwnerEmail,
            EmailConfirmed = true,
            FullName = "Владелец второго застройщика",
            OrganizationId = organization.Id
        };
        var password = configuration[OwnerPasswordVariable];
        var result = string.IsNullOrWhiteSpace(password)
            ? await userManager.CreateAsync(owner)
            : await userManager.CreateAsync(owner, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Владелец {Email} не создан: {Errors}", OwnerEmail, string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(owner, RoleNames.Owner);
        logger.LogInformation("Создан владелец {Email} организации {Organization}{Password}", OwnerEmail, organization.Name,
            string.IsNullOrWhiteSpace(password) ? " (без пароля: вход по коду из почты)" : string.Empty);
    }
}

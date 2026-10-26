using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;

namespace Sitora.Infrastructure.Data.Seed;

// Второй застройщик для разработки: организация «Rich Tower», её владелец и один дом.
// Нужен, чтобы вживую видеть разделение данных между застройщиками.
public static class RichTowerSeeder
{
    public const string Slug = "rich-tower";
    public const string OwnerEmail = "owner@rich-tower.tj";
    public const string OwnerPasswordVariable = "SEED_RICHTOWER_OWNER_PASSWORD";
    private const int Floors = 8;
    private static readonly (int Rooms, decimal Area)[] Layout = [(1, 42m), (2, 64m), (3, 88m), (2, 61m)];

    public static async Task SeedAsync(AppDbContext db, UserManager<AppUser> userManager, IConfiguration configuration, ILogger logger)
    {
        var organization = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Slug == Slug);
        if (organization is null)
        {
            organization = new Organization
            {
                Name = "Rich Tower",
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
            NameRu = "Rich Tower",
            NameTg = "Rich Tower",
            Address = "г. Душанбе, ул. Айни, 10",
            FloorsCount = Floors,
            DescriptionRu = "Жилой дом застройщика Rich Tower"
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

    // Пароль — из настройки SEED_RICHTOWER_OWNER_PASSWORD (user secrets или окружение), в репозитории его нет.
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
            FullName = "Владелец Rich Tower",
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

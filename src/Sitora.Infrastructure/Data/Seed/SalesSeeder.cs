using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Data.Seed;

// Выдуманные клиенты, сделки и брони для разработки. Телефоны и паспорта не настоящие.
public static class SalesSeeder
{
    public const int CustomersCount = 10;

    private static readonly string[] Names =
    [
        "Алиев Фарход", "Рахимова Мадина", "Саидов Джамшед", "Назарова Зарина", "Каримов Бахтиёр",
        "Юсупова Нигора", "Шарипов Рустам", "Давлатова Гулнора", "Холов Сухроб", "Мирзоева Парвина"
    ];

    public static readonly DealStage[] DealStages =
        [DealStage.Lead, DealStage.Showing, DealStage.Booking, DealStage.Contract, DealStage.Payment];

    private static readonly (string Name, string Phone)[] SiteBookings =
    [
        ("Гость с сайта 1", "+992000000101"),
        ("Гость с сайта 2", "+992000000102")
    ];

    public static async Task SeedAsync(AppDbContext db, EncryptionService encryption, TimeProvider timeProvider, ILogger logger)
    {
        if (await db.Customers.AnyAsync())
            return;

        var managerId = await db.Users.Where(u => u.Email == "manager@sitora.local" || u.Email == "admin@sitora.local")
            .OrderByDescending(u => u.Email == "manager@sitora.local")
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync();
        if (managerId is null)
        {
            logger.LogWarning("Клиенты и сделки не созданы: нет пользователя менеджера или админа");
            return;
        }

        var customers = Names.Select((name, i) => new Customer
        {
            FullName = name,
            Phone = $"+99200000{i + 1:0000}",
            Email = $"client{i + 1}@example.com",
            PassportDataEncrypted = encryption.Encrypt($"A{i + 1:0000000} (тестовые данные)")
        }).ToList();
        db.Customers.AddRange(customers);

        // Верхние этажи почти не проданы — берём свободные квартиры оттуда.
        var apartments = await db.Apartments
            .Where(a => a.Status == ApartmentStatus.Free)
            .OrderByDescending(a => a.Floor.Number).ThenBy(a => a.Number)
            .Take(DealStages.Length + SiteBookings.Length)
            .ToListAsync();

        for (var i = 0; i < DealStages.Length && i < apartments.Count; i++)
        {
            var deal = new Deal { ApartmentId = apartments[i].Id, CustomerId = customers[i].Id, ManagerId = managerId.Value };
            deal.SetPrice(apartments[i].Price, Currency.TJS, 0, null);
            if (DealStages[i] != DealStage.Lead)
                deal.ChangeStage(DealStages[i], null, apartments[i]);
            db.Deals.Add(deal);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        for (var i = 0; i < SiteBookings.Length && DealStages.Length + i < apartments.Count; i++)
        {
            var apartment = apartments[DealStages.Length + i];
            var lead = new Lead
            {
                Name = SiteBookings[i].Name,
                Phone = SiteBookings[i].Phone,
                ApartmentId = apartment.Id,
                Source = LeadSource.Website
            };
            apartment.Book();
            db.Leads.Add(lead);
            db.Bookings.Add(new Booking { ApartmentId = apartment.Id, LeadId = lead.Id, ExpiresAt = now.Add(Booking.Duration) });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Созданы клиенты ({Customers}), сделки ({Deals}) и брони с сайта ({Bookings})",
            customers.Count, Math.Min(DealStages.Length, apartments.Count), SiteBookings.Length);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Security;
using Sitora.Infrastructure.Services;

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

        var now = timeProvider.GetUtcNow().UtcDateTime;
        for (var i = 0; i < DealStages.Length && i < apartments.Count; i++)
        {
            var deal = new Deal { ApartmentId = apartments[i].Id, CustomerId = customers[i].Id, ManagerId = managerId.Value };
            deal.SetPrice(apartments[i].Price, Currency.TJS, 0, null);
            if (DealStages[i] != DealStage.Lead)
                deal.ChangeStage(DealStages[i], null, apartments[i]);
            db.Deals.Add(deal);

            if (DealStages[i] == DealStage.Payment)
                AddPlanWithPayments(db, deal, DushanbeTime.Today(now));
        }

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

    // График 30% / 24 месяца, начатый три месяца назад; оплачены взнос и два месячных платежа.
    private static void AddPlanWithPayments(AppDbContext db, Deal deal, DateOnly today)
    {
        var start = today.AddMonths(-3);
        var plan = new PaymentPlan
        {
            DealId = deal.Id,
            DownPaymentPct = 30,
            Months = 24,
            PaymentDay = 10,
            StartDate = start,
            Items = PaymentScheduleCalculator.Build(deal.FinalPrice, 30, 24, 10, start)
        };

        var paidItems = plan.Items.OrderBy(i => i.DueDate).Take(3).ToList();
        foreach (var item in paidItems)
        {
            db.Payments.Add(new Payment
            {
                DealId = deal.Id,
                Amount = item.AmountDue,
                Currency = Currency.TJS,
                AmountInDealCurrency = item.AmountDue,
                AmountTjs = item.AmountDue,
                PaidAt = item.DueDate,
                Method = item.Kind == ScheduleItemKind.DownPayment ? PaymentMethod.BankTransfer : PaymentMethod.Cash
            });
        }

        plan.ApplyPayments(paidItems.Sum(i => i.AmountDue));
        db.PaymentPlans.Add(plan);
    }
}

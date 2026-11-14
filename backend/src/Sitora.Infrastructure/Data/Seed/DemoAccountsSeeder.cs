using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Services;

namespace Sitora.Infrastructure.Data.Seed;

// Демо-доступ для жюри — только при SEED_DEMO_ACCOUNTS=true, пароли только из секретов SEED_DEMO_BUYER_PASSWORD и
// SEED_DEMO_MANAGER_PASSWORD (без пароля учётная запись не создаётся). Работает в любом окружении: на проде его запускает
// Actions → Seed prod (образ API с --migrate) после seed.sql, когда дом «Некруз Резиденс» уже есть.
//  * demo-manager@binosoz.tj — менеджер «только чтение + чат» (DemoReadOnlyMiddleware запрещает остальные изменения);
//  * demo-buyer@binosoz.tj — покупатель с одной сделкой: график платежей, два платежа, сообщения в чате, акт сверки и
//    график в PDF (настоящие, сформированы QuestPDF и учтены в «Зелёном эффекте» с числом страниц из файла).
// Покупатель входит по коду из почты, у жюри доступа к ящику нет — поэтому вход паролем, только с Auth:DemoPasswordLogin.
// Идемпотентно: повторный запуск не создаёт второй сделки, а пароли приводит к значениям из секретов.
public static class DemoAccountsSeeder
{
    public const string BuyerEmail = "demo-buyer@binosoz.tj";
    public const string ManagerEmail = "demo-manager@binosoz.tj";
    public const string BuyerPhone = "+992900000777";

    public static async Task SeedAsync(IServiceProvider provider, ILogger logger)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue("SEED_DEMO_ACCOUNTS", false))
            return;

        var db = provider.GetRequiredService<AppDbContext>();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var building = await db.Buildings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.OrganizationId == Organization.DefaultId && b.NameRu == CatalogSeeder.BuildingName);
        if (building is null)
        {
            logger.LogWarning("Демо-доступ не создан: нет дома «{Name}» (сначала заполните каталог: seed.sql)", CatalogSeeder.BuildingName);
            return;
        }

        var manager = await EnsureUserAsync(users, ManagerEmail, "Демо-менеджер", RoleNames.Manager, Organization.DefaultId,
            configuration["SEED_DEMO_MANAGER_PASSWORD"], logger);
        var buyer = await EnsureUserAsync(users, BuyerEmail, "Демо-покупатель", RoleNames.Customer, null,
            configuration["SEED_DEMO_BUYER_PASSWORD"], logger);
        if (manager is null || buyer is null)
            return;

        var customer = await db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == Organization.DefaultId && c.Email == BuyerEmail);
        if (customer is null)
        {
            customer = new Customer { OrganizationId = Organization.DefaultId, FullName = "Демо-покупатель", Phone = BuyerPhone, Email = BuyerEmail };
            db.Customers.Add(customer);
        }
        customer.UserId = buyer.Id;
        await db.SaveChangesAsync();

        if (await db.Deals.IgnoreQueryFilters().AnyAsync(d => d.CustomerId == customer.Id))
        {
            logger.LogInformation("Демо-доступ уже есть: {Buyer}, {Manager}", BuyerEmail, ManagerEmail);
            return;
        }

        // Свободная квартира без открытой сделки, с верхних этажей: сделка на этапе «Оплата» переводит её в «Продана» (как в CRM).
        var apartment = await db.Apartments.IgnoreQueryFilters().Include(a => a.Floor)
            .Where(a => a.Floor.BuildingId == building.Id && a.Status == ApartmentStatus.Free
                        && !db.Deals.IgnoreQueryFilters().Any(d => d.ApartmentId == a.Id && d.Stage != DealStage.Completed && d.Stage != DealStage.Cancelled))
            .OrderByDescending(a => a.Floor.Number).ThenBy(a => a.Number).FirstOrDefaultAsync();
        if (apartment is null)
        {
            logger.LogWarning("Демо-сделка не создана: в доме нет свободных квартир");
            return;
        }

        var timeProvider = provider.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DushanbeTime.Today(now);
        var deal = new Deal { ApartmentId = apartment.Id, CustomerId = customer.Id, ManagerId = manager.Id };
        deal.SetPrice(apartment.Price, Currency.TJS, 0, null);
        deal.ChangeStage(DealStage.Payment, null, apartment);
        db.Deals.Add(deal);

        // График 30% / 24 месяца с начала два месяца назад; оплачены взнос и первый месячный платёж.
        var start = today.AddMonths(-2);
        var plan = new PaymentPlan
        {
            DealId = deal.Id, DownPaymentPct = 30, Months = 24, PaymentDay = 10, StartDate = start,
            Items = PaymentScheduleCalculator.Build(deal.FinalPrice, 30, 24, 10, start)
        };
        var paid = plan.Items.OrderBy(i => i.DueDate).Take(2).ToList();
        foreach (var item in paid)
            db.Payments.Add(new Payment
            {
                DealId = deal.Id, Amount = item.AmountDue, Currency = Currency.TJS, AmountInDealCurrency = item.AmountDue,
                AmountTjs = item.AmountDue, PaidAt = item.DueDate,
                Method = item.Kind == ScheduleItemKind.DownPayment ? PaymentMethod.BankTransfer : PaymentMethod.Cash
            });
        plan.ApplyPayments(paid.Sum(i => i.AmountDue));
        db.PaymentPlans.Add(plan);

        db.ChatMessages.Add(new ChatMessage { DealId = deal.Id, SenderId = manager.Id, FromCustomer = false,
            Text = "Здравствуйте! Это демо-сделка: здесь график платежей, документы и чат с менеджером." });
        db.ChatMessages.Add(new ChatMessage { DealId = deal.Id, SenderId = buyer.Id, FromCustomer = true,
            Text = "Спасибо! Когда следующий платёж?" });
        provider.GetRequiredService<Sitora.Domain.Interfaces.IAuditService>()
            .Record(nameof(Deal), deal.Id, "DemoSeeded", null, new { Buyer = BuyerEmail, Manager = ManagerEmail }, Organization.DefaultId);
        await db.SaveChangesAsync();

        // Акт сверки в PDF — настоящий (QuestPDF), с числом страниц из самого файла: учитывается в «Зелёном эффекте».
        var statements = provider.GetRequiredService<StatementService>();
        var pdf = statements.Render(await statements.BuildAsync(deal.Id, today, CancellationToken.None));
        await provider.GetRequiredService<PdfIssueRecorder>().RecordAsync(deal.Id, IssuedPdfKind.Statement, pdf, CancellationToken.None);

        logger.LogInformation("Создан демо-доступ: покупатель {Buyer} (сделка по квартире {Number}), менеджер {Manager}",
            BuyerEmail, apartment.Number, ManagerEmail);
    }

    private static async Task<AppUser?> EnsureUserAsync(UserManager<AppUser> users, string email, string fullName, string role,
        Guid? organizationId, string? password, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Демо-доступ {Email} не создан: не задан пароль в секретах", email);
            return null;
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, OrganizationId = organizationId };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Не удалось создать {email}: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, role);
            return user;
        }

        // Пароль приводится к значению из секрета: сменили секрет — после следующего запуска действует новый.
        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.RemovePasswordAsync(user);
            var reset = await users.AddPasswordAsync(user, password);
            if (!reset.Succeeded)
                throw new InvalidOperationException($"Не удалось обновить пароль {email}: " + string.Join("; ", reset.Errors.Select(e => e.Description)));
        }
        return user;
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Data.Seed;

// Необязательные сотрудники демо-организации — чтобы у владельца был вход под каждой ролью (docs/ROLES_LOGIN.md).
// Менеджер отсюда — полноценный (demo-manager@binosoz.tj из DemoAccountsSeeder — только чтение и чат).
// Пара секретов <PREFIX>_EMAIL и <PREFIX>_PASSWORD (GitHub Secrets → Actions → «Seed prod», локально — user-secrets);
// нет пары — роль не создаётся. Повторный запуск не дублирует, а пароль приводит к значению из секрета (SeedPasswords).
// Руководитель дома получает доступ к дому «Некруз Резиденс»; нет дома — пропускается (сначала seed.sql).
// Почта занята пользователем другой роли или другой организации — ошибка (остальные роли всё равно обрабатываются).
public static class StaffAccountsSeeder
{
    public sealed record StaffAccount(string Prefix, string Role, string FullName)
    {
        public string EmailVariable => Prefix + "_EMAIL";
        public string PasswordVariable => Prefix + "_PASSWORD";
    }

    public static readonly StaffAccount[] Accounts =
    [
        new("SEED_STAFF_OBJECTADMIN", RoleNames.ObjectAdmin, "Руководитель дома"),
        new("SEED_STAFF_MANAGER", RoleNames.Manager, "Менеджер по продажам"),
        new("SEED_STAFF_ACCOUNTANT", RoleNames.Accountant, "Бухгалтер"),
        new("SEED_STAFF_CONTENT", RoleNames.Content, "Контент-менеджер")
    ];

    public static async Task SeedAsync(IServiceProvider provider, ILogger logger)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var db = provider.GetRequiredService<AppDbContext>();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var audit = provider.GetRequiredService<IAuditService>();
        var timeProvider = provider.GetRequiredService<TimeProvider>();
        var errors = new List<string>();

        foreach (var account in Accounts)
        {
            var email = configuration[account.EmailVariable]?.Trim().ToLowerInvariant();
            var password = configuration[account.PasswordVariable];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                if (!string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(password))
                    logger.LogWarning("Сотрудник {Role:l}: пропущен — нужны оба секрета {EmailVariable:l} и {PasswordVariable:l}",
                        account.Role, account.EmailVariable, account.PasswordVariable);
                else
                    logger.LogInformation("Сотрудник {Role:l}: пропущен (нет секрета {EmailVariable:l})", account.Role, account.EmailVariable);
                continue;
            }

            try
            {
                await EnsureAsync(db, users, audit, timeProvider, account, email, password, logger);
            }
            catch (InvalidOperationException ex)
            {
                db.ChangeTracker.Clear();
                errors.Add(ex.Message);
                logger.LogError("Сотрудник {Role:l} {Email:l}: {Error:l}", account.Role, email, ex.Message);
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Сотрудники из секретов сидера: " + string.Join(" | ", errors));
    }

    private static async Task EnsureAsync(AppDbContext db, UserManager<AppUser> users, IAuditService audit, TimeProvider timeProvider,
        StaffAccount account, string email, string password, ILogger logger)
    {
        Guid? buildingId = null;
        if (account.Role == RoleNames.ObjectAdmin)
        {
            buildingId = await db.Buildings.IgnoreQueryFilters()
                .Where(b => b.OrganizationId == Organization.DefaultId && b.NameRu == CatalogSeeder.BuildingName)
                .Select(b => (Guid?)b.Id).FirstOrDefaultAsync();
            if (buildingId is null)
            {
                logger.LogWarning("Сотрудник {Role:l} {Email:l}: пропущен — нет дома «{Building:l}» (сначала seed.sql)",
                    account.Role, email, CatalogSeeder.BuildingName);
                return;
            }
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = account.FullName, OrganizationId = Organization.DefaultId };
            await using var transaction = await db.Database.BeginTransactionAsync();
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException($"не удалось создать (секрет {account.PasswordVariable}): " + SeedPasswords.Describe(created.Errors));
            await users.AddToRoleAsync(user, account.Role);
            if (buildingId is not null)
                db.UserBuildings.Add(new UserBuilding { UserId = user.Id, BuildingId = buildingId.Value });
            audit.Record(nameof(AppUser), user.Id, SeedPasswords.CreatedAction, null,
                new { user.Email, account.Role, Secret = account.EmailVariable }, Organization.DefaultId);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Сотрудник {Role:l} {Email:l}: создан", account.Role, email);
            return;
        }

        var roles = await users.GetRolesAsync(user);
        if (user.OrganizationId != Organization.DefaultId || roles.Count != 1 || roles[0] != account.Role)
            throw new InvalidOperationException(
                $"почта {email} из секрета {account.EmailVariable} уже занята другим пользователем ({string.Join(", ", roles)}) — укажите другую почту");

        if (buildingId is not null && !await db.UserBuildings.AnyAsync(x => x.UserId == user.Id))
        {
            db.UserBuildings.Add(new UserBuilding { UserId = user.Id, BuildingId = buildingId.Value });
            await db.SaveChangesAsync();
        }

        var changed = await SeedPasswords.SyncAsync(db, users, audit, timeProvider, user, password, account.PasswordVariable);
        logger.LogInformation(changed ? "Сотрудник {Role:l} {Email:l}: пароль обновлён" : "Сотрудник {Role:l} {Email:l}: уже есть, пароль совпадает",
            account.Role, email);
    }
}

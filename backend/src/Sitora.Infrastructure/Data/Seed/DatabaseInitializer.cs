using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Data.Seed;

public static class DatabaseInitializer
{
    private static readonly (string Email, string FullName, string Role, string PasswordVariable)[] DevelopmentUsers =
    [
        ("admin@sitora.local", "Владелец", RoleNames.Owner, "SEED_ADMIN_PASSWORD"),
        ("manager@sitora.local", "Менеджер по продажам", RoleNames.Manager, "SEED_MANAGER_PASSWORD"),
        ("accountant@sitora.local", "Бухгалтер", RoleNames.Accountant, "SEED_ACCOUNTANT_PASSWORD"),
        ("content@sitora.local", "Контент-менеджер", RoleNames.Content, "SEED_CONTENT_PASSWORD")
    ];

    public const string MigrateCommand = "--migrate";

    // Обычный старт без миграций (Production): база должна быть уже обновлена командой --migrate.
    // Падаем сразу и понятно, а не на первом запросе к несуществующей таблице.
    public static async Task EnsureDatabaseUpToDateAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException(
                $"Нет подключения к базе или она ещё не создана. Примените миграции: dotnet Sitora.WebApi.dll {MigrateCommand}");

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
            throw new InvalidOperationException(
                $"В базе есть неприменённые миграции ({string.Join(", ", pending)}). " +
                $"Примените их: dotnet Sitora.WebApi.dll {MigrateCommand}");
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        var db = provider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SeedRolesAsync(provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>());
        await BootstrapOwnerAsync(provider.GetRequiredService<UserManager<AppUser>>(),
            provider.GetRequiredService<IConfiguration>(), logger);
        await EnsurePlatformOwnerAsync(provider.GetRequiredService<UserManager<AppUser>>(),
            provider.GetRequiredService<IConfiguration>(), logger);

        if (provider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            await SeedDevelopmentUsersAsync(
                provider.GetRequiredService<UserManager<AppUser>>(),
                provider.GetRequiredService<IConfiguration>(),
                logger);
            await CatalogSeeder.SeedAsync(db, logger);
            await SalesSeeder.SeedAsync(db,
                provider.GetRequiredService<EncryptionService>(),
                provider.GetRequiredService<TimeProvider>(),
                logger);
            await ConstructionSeeder.SeedAsync(db);
            // Второй застройщик; тесты его выключают (Seed:RichTower=false), чтобы данные сидера были предсказуемы.
            if (provider.GetRequiredService<IConfiguration>().GetValue("Seed:RichTower", true))
                await RichTowerSeeder.SeedAsync(db, provider.GetRequiredService<UserManager<AppUser>>(),
                    provider.GetRequiredService<IConfiguration>(), logger);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    // Первый владелец организации по умолчанию в любом окружении: создаётся, только пока владельцев нет.
    // После первого запуска переменные BOOTSTRAP_ADMIN_* можно убрать из .env.
    private static async Task BootstrapOwnerAsync(UserManager<AppUser> userManager, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["BOOTSTRAP_ADMIN_EMAIL"];
        var password = configuration["BOOTSTRAP_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;
        if ((await userManager.GetUsersInRoleAsync(RoleNames.Owner)).Count > 0)
            return;

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Владелец",
            OrganizationId = Organization.DefaultId
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Не удалось создать первого владельца: " +
                                                string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, RoleNames.Owner);
        logger.LogInformation("Создан первый владелец {Email}", email);
    }

    // Создатель платформы — только из .env (ADMIN_EMAIL). Вход: код из почты и обязательно второй фактор,
    // поэтому без пароля (ADMIN_PASSWORD) учётная запись не создаётся: войти было бы нечем.
    // TOTP можно включить потом в разделе платформы. Существующему пароль не меняется.
    private static async Task EnsurePlatformOwnerAsync(UserManager<AppUser> userManager, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return;

        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            if (user.OrganizationId is not null)
                throw new InvalidOperationException($"ADMIN_EMAIL {email} уже занят сотрудником организации");
            if (!await userManager.IsInRoleAsync(user, RoleNames.PlatformOwner))
                await userManager.AddToRoleAsync(user, RoleNames.PlatformOwner);
            return;
        }

        var password = configuration["ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Создатель платформы {Email} не создан: задайте ADMIN_PASSWORD (второй фактор входа)", email);
            return;
        }

        user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Создатель платформы" };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Не удалось создать создателя платформы: " +
                                                string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, RoleNames.PlatformOwner);
        logger.LogInformation("Создан создатель платформы {Email}", email);
    }

    private static async Task SeedDevelopmentUsersAsync(UserManager<AppUser> userManager, IConfiguration configuration, ILogger logger)
    {
        foreach (var (email, fullName, role, passwordVariable) in DevelopmentUsers)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
                continue;

            var password = configuration[passwordVariable];
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Пользователь {Email} не создан: не задана переменная окружения {Variable}", email, passwordVariable);
                continue;
            }

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                OrganizationId = Organization.DefaultId
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Не удалось создать пользователя {email}: {errors}");
            }

            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("Создан пользователь {Email} с ролью {Role}", email, role);
        }
    }
}

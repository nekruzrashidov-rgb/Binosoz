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
        ("admin@sitora.local", "Администратор", RoleNames.Admin, "SEED_ADMIN_PASSWORD"),
        ("manager@sitora.local", "Менеджер по продажам", RoleNames.Manager, "SEED_MANAGER_PASSWORD"),
        ("accountant@sitora.local", "Бухгалтер", RoleNames.Accountant, "SEED_ACCOUNTANT_PASSWORD"),
        ("content@sitora.local", "Контент-менеджер", RoleNames.Content, "SEED_CONTENT_PASSWORD")
    ];

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        var db = provider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SeedRolesAsync(provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>());

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

            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName };
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

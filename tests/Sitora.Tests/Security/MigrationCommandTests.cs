using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Sitora.Infrastructure.Data;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Production-настройки без применения миграций при старте, как на сервере.
public class NoAutoMigrateFactory : SitoraApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
    }
}

public class MigrationCommandTests(NoAutoMigrateFactory factory) : IClassFixture<NoAutoMigrateFactory>
{
    [Fact]
    public async Task Production_RefusesToStartWithoutMigrations_AndMigrateCommandPreparesDatabase()
    {
        // Без миграций API не стартует и подсказывает команду.
        var startError = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("--migrate", startError.ToString());

        // Команда деплоя — тот же исполняемый файл с аргументом --migrate.
        var (exitCode, output) = await RunMigrateAsync(factory.ConnectionString);
        Assert.True(exitCode == 0, output);

        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(factory.ConnectionString).Options, TimeProvider.System);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(["Accountant", "Admin", "Content", "Customer", "Manager"],
            await db.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync());

        // Production не запускает сидер разработки.
        Assert.False(await db.Buildings.AnyAsync());
        Assert.False(await db.Users.AnyAsync());
    }

    private static async Task<(int ExitCode, string Output)> RunMigrateAsync(string connectionString)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Sitora.WebApi.dll"));
        start.ArgumentList.Add("--migrate");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["ConnectionStrings__Default"] = connectionString;
        start.Environment["Jwt__Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        start.Environment["Encryption__Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        return (process.ExitCode, await stdout + await stderr);
    }
}

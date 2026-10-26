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
        // Без миграций API не стартует: код выхода 1 и подсказка с командой в логе, без стека исключения.
        var (startExitCode, startOutput) = await RunApiAsync(factory.ConnectionString);
        Assert.True(startExitCode == 1, startOutput);
        Assert.Contains("--migrate", startOutput);
        Assert.DoesNotContain("Unhandled exception", startOutput);

        // Команда деплоя — тот же исполняемый файл с аргументом --migrate.
        var (exitCode, output) = await RunApiAsync(factory.ConnectionString, "--migrate");
        Assert.True(exitCode == 0, output);

        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(factory.ConnectionString).Options, TimeProvider.System);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(["Accountant", "Content", "Customer", "Manager", "ObjectAdmin", "Owner", "PlatformOwner"],
            await db.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync());

        // Production не запускает сидер разработки.
        Assert.False(await db.Buildings.AnyAsync());
        Assert.False(await db.Users.AnyAsync());
    }

    // Запускает API отдельным процессом с Production-настройками, как в контейнере.
    private static async Task<(int ExitCode, string Output)> RunApiAsync(string connectionString, params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Sitora.WebApi.dll"));
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_HTTP_PORTS"] = Random.Shared.Next(20000, 30000).ToString();
        start.Environment["ConnectionStrings__Default"] = connectionString;
        start.Environment["Jwt__Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        start.Environment["Encryption__Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        }
        catch (TimeoutException)
        {
            // API неожиданно запустился и работает — останавливаем, тест упадёт на коде выхода.
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        return (process.ExitCode, await stdout + await stderr);
    }
}

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sitora.Domain.Enums;

namespace Sitora.Tests.Infrastructure;

// backend/scripts/seed.sql — демо-каталог для пустой базы прода (Render). Прогоняется на базе после всех миграций:
// так тест ловит расхождение скрипта со схемой. Повторный запуск ничего не дублирует, сам запуск — в AuditLog.
public class SeedScriptTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private const string SeedName = "Некруз Резиденс";

    [Fact]
    public async Task SeedSql_RunsOnCurrentSchema_IsIdempotent_AndAudited()
    {
        // Сидер разработки уже завёл дом с тем же названием: переименовываем его, чтобы скрипт считал базу пустой.
        await factory.WithDbAsync(async db =>
        {
            await db.Buildings.IgnoreQueryFilters().Where(b => b.NameRu == SeedName)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.NameRu, "Дом сидера разработки"));
        });

        var sql = await File.ReadAllTextAsync(FindSeedScript());
        await RunAsync(sql);
        await RunAsync(sql);

        var result = await factory.WithDbAsync(async db =>
        {
            var buildings = await db.Buildings.IgnoreQueryFilters().Where(b => b.NameRu == SeedName).Select(b => b.Id).ToListAsync();
            var id = buildings.Single();
            return new
            {
                Buildings = buildings.Count,
                Floors = await db.Floors.IgnoreQueryFilters().CountAsync(f => f.BuildingId == id),
                Apartments = await db.Apartments.IgnoreQueryFilters().CountAsync(a => a.Floor.BuildingId == id),
                Sold = await db.Apartments.IgnoreQueryFilters().CountAsync(a => a.Floor.BuildingId == id && a.Status == ApartmentStatus.Sold),
                Booked = await db.Bookings.IgnoreQueryFilters().CountAsync(b => b.Apartment.Floor.BuildingId == id && b.Status == BookingStatus.Active),
                Audit = await db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.EntityId == id && a.Action == "Seeded"),
                Prices = await db.Apartments.IgnoreQueryFilters().Where(a => a.Floor.BuildingId == id).Select(a => a.Price).Distinct().ToListAsync()
            };
        });

        Assert.Equal(1, result.Buildings);
        Assert.Equal(21, result.Floors);
        Assert.Equal(160, result.Apartments);
        Assert.Equal(6, result.Sold);
        Assert.Equal(5, result.Booked);
        Assert.Equal(1, result.Audit);
        // ROUND(area × price / 100) × 100 — как Apartment.CalculatePrice: 42.5 × 7500, 68 × 7000, 92 × 6800, 118.5 × 6500.
        Assert.Equal([318_800m, 476_000m, 625_600m, 770_300m], result.Prices.Order());

        // «Зелёная» часть: те же демо-значения, что у сидера разработки (GreenDemoSeeder), организация помечена демо.
        var green = await factory.WithDbAsync(async db => new
        {
            Eco = await db.Buildings.IgnoreQueryFilters().AsNoTracking().Where(b => b.NameRu == SeedName).Select(b => b.Eco).SingleAsync(),
            Org = await db.Organizations.IgnoreQueryFilters().AsNoTracking().Where(o => o.Id == Sitora.Domain.Entities.Organization.DefaultId).Select(o => o.Green).SingleAsync()
        });
        var expected = Sitora.Infrastructure.Data.Seed.GreenDemoSeeder.Eco();
        Assert.NotNull(green.Eco);
        Assert.Equivalent(expected, green.Eco);
        Assert.Equal(Sitora.Infrastructure.Data.Seed.GreenDemoSeeder.TariffKwhTjs, green.Org.TariffKwhTjs);
        Assert.Equal(Sitora.Infrastructure.Data.Seed.GreenDemoSeeder.BaselineKwhPerM2Year, green.Org.BaselineKwhPerM2Year);
        Assert.Equal(Sitora.Infrastructure.Data.Seed.GreenDemoSeeder.BaselineSource, green.Org.BaselineSource);
        Assert.True(green.Org.IsDemo);
        Assert.Null(green.Org.Co2KgPerKgPaper);
    }

    // Текст — файл backend/scripts/seed.sql из репозитория, не ввод пользователя.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "SQL из файла репозитория")]
    private async Task RunAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string FindSeedScript()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "scripts", "seed.sql");
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException("Не найден backend/scripts/seed.sql");
    }
}

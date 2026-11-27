using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Ai;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Прод-сценарий seed.sql — своя фикстура (своя временная база): тест сам делает базу «как на проде» — организация и дом
// «Некруз Резиденс» на сайте, сделка и платёж в TJS, блоков нет — и проходит один и в любом порядке с другими.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "Тест: SQL — файл backend/scripts/seed.sql из репозитория.")]
public class RichTowerProdSeedTests(ProdLikeFactory factory) : IClassFixture<ProdLikeFactory>
{
    [Fact]
    public async Task SeedSql_OnProdLikeDb_CreatesBlocks_HidesOldBuilding_DealsAndPaymentsUntouched_Idempotent()
    {
        // Как на проде: организация и дом со старыми именами, сделка с платежом в TJS.
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var accountant = await factory.CreateAccountantClientAsync();
        (await TestScenarios.PayAsync(accountant, deal.DealId, 1000m)).EnsureSuccessStatusCode();
        var oldId = await factory.WithDbAsync(db => db.Deals.IgnoreQueryFilters().Where(d => d.Id == deal.DealId)
            .Select(d => d.Apartment.Floor.BuildingId).SingleAsync());
        await SeedSqlDb.ExecAsync(factory.ConnectionString, $"UPDATE \"Buildings\" SET \"NameRu\" = 'Некруз Резиденс', \"NameTg\" = 'Некруз Резиденс' WHERE \"Id\" = '{oldId}'");
        await SeedSqlDb.ExecAsync(factory.ConnectionString, $"UPDATE \"Organizations\" SET \"Name\" = 'Некруз Резиденс' WHERE \"Id\" = '{Organization.DefaultId}'");
        var before = await SeedSqlDb.SnapshotAsync(factory.ConnectionString, deal.DealId, oldId);

        var sql = await File.ReadAllTextAsync(SeedSqlDb.FindSeedSql());
        await SeedSqlDb.ExecAsync(factory.ConnectionString, sql);
        await SeedSqlDb.ExecAsync(factory.ConnectionString, sql);

        var after = await SeedSqlDb.SnapshotAsync(factory.ConnectionString, deal.DealId, oldId);
        Assert.Equal(before.Deal, after.Deal);
        Assert.Equal(before.Payments, after.Payments);
        Assert.Equal(before.Apartments, after.Apartments);

        var result = await factory.WithDbAsync(async db => new
        {
            Blocks = await db.Buildings.IgnoreQueryFilters().Where(b => b.OrganizationId == Organization.DefaultId && b.BlockCode != null)
                .OrderBy(b => b.SortOrder).Select(b => new { b.BlockCode, b.FloorsCount, b.PriceCurrency, Units = b.Floors.SelectMany(f => f.Apartments).Count() }).ToListAsync(),
            Old = await db.Buildings.IgnoreQueryFilters().Where(b => b.Id == oldId).Select(b => new { b.NameRu, b.IsPublished }).SingleAsync(),
            Hidden = await db.AuditLogs.IgnoreQueryFilters().CountAsync(l => l.EntityId == oldId && l.Action == RichTowerBlocksSql.UnpublishedAction),
            NotFree = await db.Apartments.IgnoreQueryFilters().CountAsync(a => a.Floor.Building.BlockCode != null && a.Status != ApartmentStatus.Free),
            Plan = await db.Organizations.IgnoreQueryFilters().Where(o => o.Id == Organization.DefaultId).Select(o => o.SitePlan).SingleAsync()
        });
        Assert.Equal(["A", "B", "C"], result.Blocks.Select(b => b.BlockCode));
        Assert.Equal([22, 20, 20], result.Blocks.Select(b => b.FloorsCount));
        Assert.All(result.Blocks, b => Assert.Equal(Currency.USD, b.PriceCurrency));
        Assert.Equal(("Rich Tower", false), (result.Old.NameRu, result.Old.IsPublished));
        Assert.Equal(1, result.Hidden);
        Assert.Equal("3", await SeedSqlDb.ScalarAsync(factory.ConnectionString, "SELECT count(*) FROM \"AuditLog\" WHERE \"Entity\" = 'Building' AND \"Action\" = 'Seeded' AND \"After\"->>'source' = 'scripts/seed.sql' AND \"After\" ? 'blockCode'"));
        Assert.Equal(0, result.NotFree);
        Assert.NotNull(result.Plan);
        // Помещения те же, что у сидера разработки.
        var expected = RichTowerBlocksSeeder.Blocks.Select(s => RichTowerBlocksSeeder.NewBlock(s, Organization.DefaultId, null).Floors.Sum(f => f.Apartments.Count));
        Assert.Equal(expected, result.Blocks.Select(b => b.Units));

        // На сайте — только A/B/C; в бренде тоже.
        var site = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        Assert.Equal(["A", "B", "C"], site!.Items.Select(b => b.BlockCode));
        var brand = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/public/brand?slug=nekruz");
        Assert.Equal(3, brand.GetProperty("buildings").GetArrayLength());
        // Сотрудник видит прежний дом в CRM, с пометкой «скрыт с сайта».
        var staff = await (await factory.CreateManagerClientAsync()).GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?pageSize=100", TestJson.Options);
        Assert.False(staff!.Items.Single(b => b.Id == oldId).IsPublished);
    }
}

// Запуск SQL на временной базе тестов: seed.sql, снимки строк сделки, платежей и квартир.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "Тест: SQL — константы и файл backend/scripts/seed.sql из репозитория.")]
public static class SeedSqlDb
{
    public static async Task<(string Deal, string Payments, string Apartments)> SnapshotAsync(string cs, Guid dealId, Guid buildingId) =>
        (await ScalarAsync(cs, $"SELECT row_to_json(d)::text FROM (SELECT \"Price\", \"Discount\", \"Currency\", \"Stage\", \"ApartmentId\", \"CustomerId\" FROM \"Deals\" WHERE \"Id\" = '{dealId}') d"),
         await ScalarAsync(cs, $"SELECT coalesce(json_agg(p ORDER BY p.\"Id\")::text, '') FROM (SELECT \"Id\", \"Amount\", \"Currency\", \"PaidAt\", \"ReversalOfId\" FROM \"Payments\" WHERE \"DealId\" = '{dealId}') p"),
         await ScalarAsync(cs, $"SELECT json_agg(a ORDER BY a.\"Id\")::text FROM (SELECT a.\"Id\", a.\"Price\", a.\"Currency\", a.\"Status\" FROM \"Apartments\" a JOIN \"Floors\" f ON f.\"Id\" = a.\"FloorId\" WHERE f.\"BuildingId\" = '{buildingId}') a"));

    public static async Task ExecAsync(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<string> ScalarAsync(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? string.Empty;
    }

    public static string FindSeedSql()
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

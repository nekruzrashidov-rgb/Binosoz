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

// Прод: база с прежним домом «Некруз Резиденс» (сделка и платёж в TJS), блоков ещё нет — сидер разработки их не создаёт.
public class ProdLikeFactory : AiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Seed:RichTowerBlocks", "false");
    }
}

// backend/scripts/seed.sql на проде: блоки A/B/C создаются как новые дома, прежний дом скрыт с сайта, его сделки и
// платежи не меняются, повторный запуск ничего не дублирует; покупатель скрытого дома видит свою сделку и спрашивает AI.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "Тест: SQL — файл backend/scripts/seed.sql из репозитория.")]
public class RichTowerBlocksSqlTests(ProdLikeFactory factory) : IClassFixture<ProdLikeFactory>
{
    [Fact]
    public void SeedSql_GeneratedBlocksSection_IsUpToDate()
    {
        var path = FindSeedSql();
        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var generated = RichTowerBlocksSql.Generate();
        var a = text.IndexOf(RichTowerBlocksSql.Begin, StringComparison.Ordinal);
        var b = text.IndexOf(RichTowerBlocksSql.End, StringComparison.Ordinal);
        if (Environment.GetEnvironmentVariable("UPDATE_SEED_SQL") == "1")
        {
            text = a >= 0 && b > a ? text[..a] + generated + text[(b + RichTowerBlocksSql.End.Length + 1)..] : text.TrimEnd() + "\n\n" + generated;
            File.WriteAllText(path, text.Replace("\n", "\r\n", StringComparison.Ordinal));
            return;
        }
        Assert.True(a >= 0 && b > a, "в seed.sql нет блока Rich Tower: UPDATE_SEED_SQL=1 dotnet test --filter RichTowerBlocksSql");
        Assert.Equal(generated, text[a..(b + RichTowerBlocksSql.End.Length + 1)]);
    }

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
        await Exec($"UPDATE \"Buildings\" SET \"IsPublished\" = true WHERE \"Id\" = '{oldId}'"); // на проде дом на сайте
        await Exec($"UPDATE \"Buildings\" SET \"NameRu\" = 'Некруз Резиденс', \"NameTg\" = 'Некруз Резиденс' WHERE \"Id\" = '{oldId}'");
        await Exec($"UPDATE \"Organizations\" SET \"Name\" = 'Некруз Резиденс' WHERE \"Id\" = '{Organization.DefaultId}'");
        var before = await SnapshotAsync(deal.DealId, oldId);

        var sql = await File.ReadAllTextAsync(FindSeedSql());
        await Exec(sql);
        await Exec(sql);

        var after = await SnapshotAsync(deal.DealId, oldId);
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
        Assert.Equal("3", await Scalar("SELECT count(*) FROM \"AuditLog\" WHERE \"Entity\" = 'Building' AND \"Action\" = 'Seeded' AND \"After\"->>'source' = 'scripts/seed.sql' AND \"After\" ? 'blockCode'"));
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

    // Дом со сделкой покупателя, скрытый с сайта (IsPublished = false); кэш контекста ЖК сброшен.
    private async Task<(DealScenario Deal, Guid BuildingId, Guid ApartmentId, string Name)> HiddenBuildingWithDealAsync()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var x = await factory.WithDbAsync(db => db.Deals.IgnoreQueryFilters().Where(d => d.Id == deal.DealId)
            .Select(d => new { d.Apartment.Floor.BuildingId, d.ApartmentId, d.Apartment.Floor.Building.NameRu }).SingleAsync());
        await Exec($"UPDATE \"Buildings\" SET \"IsPublished\" = false WHERE \"Id\" = '{x.BuildingId}'");
        await factory.WithServiceAsync<Microsoft.Extensions.Caching.Memory.IMemoryCache>(c => { c.Remove(Sitora.Infrastructure.Services.EstateContext.CacheKey(Organization.DefaultId)); return Task.CompletedTask; });
        return (deal, x.BuildingId, x.ApartmentId, x.NameRu);
    }

    private static string BuildingLine(string name) => "Building \"" + name + "\"";

    private string EstatePart()
    {
        var system = factory.Model.LastSystem!;
        return system[system.IndexOf(Sitora.Infrastructure.Services.EstateContext.Header, StringComparison.Ordinal)..];
    }

    // 1. Покупатель со сделкой в скрытом доме: кабинет — как раньше, AI в кабинете знает его дом.
    [Fact]
    public async Task BuyerWithDealInHiddenBuilding_SeesDealAndApartment_AiContextHasTheBuilding()
    {
        var (deal, oldId, aptId, name) = await HiddenBuildingWithDealAsync();
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var mine = await customer.GetFromJsonAsync<JsonElement>("/api/v1/me/deals");
        Assert.Contains(mine.EnumerateArray(), d => d.GetProperty("id").GetGuid() == deal.DealId);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/v1/deals/{deal.DealId}/schedule")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/v1/deals/{deal.DealId}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/v1/apartments/{aptId}")).StatusCode);
        var site = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        Assert.DoesNotContain(site!.Items, b => b.Id == oldId);

        (await customer.PostAsJsonAsync("/api/v1/ai/chat", new AiChatRequest("Какой у меня дом?", null))).EnsureSuccessStatusCode();
        Assert.Contains(BuildingLine(name), EstatePart());
    }

    // 2. Гость на сайте: AI-помощник гостя о скрытом доме не знает.
    [Fact]
    public async Task Guest_AiContext_HasNoHiddenBuilding()
    {
        var (_, _, _, name) = await HiddenBuildingWithDealAsync();
        var reply = await factory.CreateClient().PostAsJsonAsync("/api/v1/public/nekruz/ai/chat", new { message = "Какие дома есть?", history = Array.Empty<object>() });
        reply.EnsureSuccessStatusCode();
        Assert.DoesNotContain(BuildingLine(name), EstatePart());
    }

    // 3. Другой покупатель, без сделки в этом доме: в его кабинете скрытого дома в контексте нет.
    [Fact]
    public async Task OtherBuyerWithoutDealThere_AiContext_HasNoHiddenBuilding()
    {
        var (_, _, _, name) = await HiddenBuildingWithDealAsync();
        var manager = await factory.CreateManagerClientAsync();
        var phone = SitoraApiFactory.RandomPhone();
        await TestScenarios.ReadAsync<Sitora.Domain.DTOs.Sales.CustomerResponse>(await manager.PostAsJsonAsync("/api/v1/customers",
            new Sitora.Domain.DTOs.Sales.CreateCustomerRequest("Другой покупатель", phone, null, null, null)));
        var other = await factory.CreateCustomerClientAsync(phone);
        (await other.PostAsJsonAsync("/api/v1/ai/chat", new AiChatRequest("Какие дома есть?", null))).EnsureSuccessStatusCode();
        Assert.DoesNotContain(BuildingLine(name), EstatePart());
    }

    private async Task<(string Deal, string Payments, string Apartments)> SnapshotAsync(Guid dealId, Guid buildingId) =>
        (await Scalar($"SELECT row_to_json(d)::text FROM (SELECT \"Price\", \"Discount\", \"Currency\", \"Stage\", \"ApartmentId\", \"CustomerId\" FROM \"Deals\" WHERE \"Id\" = '{dealId}') d"),
         await Scalar($"SELECT coalesce(json_agg(p ORDER BY p.\"Id\")::text, '') FROM (SELECT \"Id\", \"Amount\", \"Currency\", \"PaidAt\", \"ReversalOfId\" FROM \"Payments\" WHERE \"DealId\" = '{dealId}') p"),
         await Scalar($"SELECT json_agg(a ORDER BY a.\"Id\")::text FROM (SELECT a.\"Id\", a.\"Price\", a.\"Currency\", a.\"Status\" FROM \"Apartments\" a JOIN \"Floors\" f ON f.\"Id\" = a.\"FloorId\" WHERE f.\"BuildingId\" = '{buildingId}') a"));

    private async Task Exec(string sql)
    {
        await using var c = new NpgsqlConnection(factory.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string> Scalar(string sql)
    {
        await using var c = new NpgsqlConnection(factory.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? string.Empty;
    }

    private static string FindSeedSql()
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

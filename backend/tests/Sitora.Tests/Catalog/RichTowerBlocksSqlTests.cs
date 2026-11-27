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
        var path = SeedSqlDb.FindSeedSql();
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

    private Task Exec(string sql) => SeedSqlDb.ExecAsync(factory.ConnectionString, sql);
}

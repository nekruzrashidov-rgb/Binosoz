using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Цена за м² по этажу (правило блока, USD): ступени, пентхаусы, «Пересчитать цены» только для свободных, AuditLog.
public class PriceRuleTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private Task<Building> BlockAsync(string code) =>
        factory.WithDbAsync(db => db.Buildings.AsNoTracking().FirstAsync(b => b.BlockCode == code));

    [Fact]
    public void DefaultRule_3to6_1200_DownTo900On19_ThenFlat_PenthouseOwn()
    {
        var b = new Building { FloorsCount = 22, PriceSteps = PriceDefaults.Steps(22), PenthousePricePerM2 = PriceDefaults.PenthousePricePerM2 };
        decimal Ppm(int f) => PriceRules.PricePerM2(b, f, f == b.FloorsCount)!.Value;
        Assert.Equal(1200m, Ppm(3));
        Assert.Equal(1200m, Ppm(6));
        Assert.Equal(900m, Ppm(19));
        Assert.Equal(900m, Ppm(20));
        Assert.Equal(900m, Ppm(21));
        Assert.Equal(1400m, Ppm(22));
        Assert.InRange(Ppm(12), 901m, 1199m);
        for (var f = 6; f < 19; f++)
        {
            Assert.True(Ppm(f + 1) < Ppm(f), $"этаж {f + 1}");
            // ≈ −23,08 $ за этаж (300 / 13), цена м² — до 1 $.
            Assert.InRange(Ppm(f) - Ppm(f + 1), 22m, 24m);
            Assert.Equal(decimal.Round(Ppm(f)), Ppm(f));
        }
    }

    [Fact]
    public async Task SeededBlocks_PricedByRule_InUsd()
    {
        // Блок B меняет тест «Пересчитать цены» (одна база на класс), поэтому сид сверяется по A и C.
        foreach (var code in new[] { "A", "C" })
        {
            var b = await BlockAsync(code);
            var apts = await factory.WithDbAsync(db => db.Apartments.AsNoTracking().Include(a => a.Floor).Where(a => a.Floor.BuildingId == b.Id).ToListAsync());
            Assert.All(apts, a => Assert.Equal(Currency.USD, a.Currency));
            foreach (var (floor, ppm) in new[] { (3, 1200m), (6, 1200m), (19, 900m), (b.FloorsCount, 1400m) })
                Assert.All(apts.Where(a => a.Floor.Number == floor), a => Assert.Equal(Apartment.CalculatePrice(a.Area, ppm), a.Price));
        }
    }

    [Fact]
    public async Task Apply_ChangesOnlyFree_AuditInSameSave_CatalogShowsNewPrice()
    {
        var b = await BlockAsync("B");
        var admin = await factory.CreateAdminClientAsync();
        // Одна квартира 5-го этажа забронирована, одна продана — их цены не меняются.
        var (bookedId, soldId) = await factory.WithDbAsync(async db =>
        {
            var free = await db.Apartments.Where(a => a.Floor.BuildingId == b.Id && a.Floor.Number == 5 && a.Status == ApartmentStatus.Free).OrderBy(a => a.Number).Take(2).ToListAsync();
            free[0].Book(); free[1].Sell();
            await db.SaveChangesAsync();
            return (free[0].Id, free[1].Id);
        });
        var before = await factory.WithDbAsync(db => db.Apartments.AsNoTracking().Where(a => a.Id == bookedId || a.Id == soldId).ToDictionaryAsync(a => a.Id, a => a.Price));

        var rule = await admin.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/price-rule", new
        {
            currency = "USD", penthousePricePerM2 = 1500m, steps = new[] { new { fromFloor = 1, toFloor = 19, pricePerM2 = 1000m } }
        });
        Assert.Equal(HttpStatusCode.OK, rule.StatusCode);
        var applied = await TestScenarios.ReadAsync<ApplyPriceRuleResponse>(await admin.PostAsync($"/api/v1/buildings/{b.Id}/price-rule/apply", null));
        Assert.True(applied.Updated > 0);
        Assert.True(applied.Skipped >= 2);

        var after = await factory.WithDbAsync(db => db.Apartments.AsNoTracking().Include(a => a.Floor).Where(a => a.Floor.BuildingId == b.Id).ToListAsync());
        Assert.Equal(before[bookedId], after.Single(a => a.Id == bookedId).Price);
        Assert.Equal(before[soldId], after.Single(a => a.Id == soldId).Price);
        Assert.All(after.Where(a => a.Kind == ApartmentKind.Residential && a.Status == ApartmentStatus.Free && a.Floor.Number < b.FloorsCount),
            a => Assert.Equal(Apartment.CalculatePrice(a.Area, 1000m), a.Price));
        Assert.All(after.Where(a => a.Kind != ApartmentKind.Parking && a.Status == ApartmentStatus.Free && a.Floor.Number == b.FloorsCount),
            a => Assert.Equal(Apartment.CalculatePrice(a.Area, 1500m), a.Price));
        var changed = after.First(a => a.Kind == ApartmentKind.Residential && a.Status == ApartmentStatus.Free && a.Floor.Number == 5);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.EntityId == changed.Id && l.Action == "PriceChanged")));
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.EntityId == b.Id && l.Action == "PriceRuleUpdated")));
        // Кэш каталога сброшен: гость видит новую цену.
        var floor = await factory.CreateClient().GetFromJsonAsync<PagedResult<FloorResponse>>($"/api/v1/buildings/{b.Id}/floors?pageSize=100", TestJson.Options);
        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<ApartmentResponse>>(
            $"/api/v1/floors/{floor!.Items.Single(f => f.Number == 5).Id}/apartments", TestJson.Options);
        Assert.Equal(Apartment.CalculatePrice(changed.Area, 1000m), list!.Items.Single(a => a.Id == changed.Id).Price);
    }

    [Fact]
    public async Task InvalidRule_Is400_ContentForbidden_DealTakesCatalogCurrency()
    {
        var b = await BlockAsync("C");
        var admin = await factory.CreateAdminClientAsync();
        var overlap = await admin.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/price-rule", new
        {
            currency = "USD", steps = new[] { new { fromFloor = 3, toFloor = 10, pricePerM2 = 1200m }, new { fromFloor = 8, toFloor = 19, pricePerM2 = 900m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        Assert.Contains("Ступени цены не должны пересекаться по этажам", await overlap.Content.ReadAsStringAsync());
        var negative = await admin.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/price-rule", new { currency = "USD", steps = new[] { new { fromFloor = 3, toFloor = 5, pricePerM2 = -1m } } });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        var content = await factory.CreateContentClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await content.PostAsync($"/api/v1/buildings/{b.Id}/price-rule/apply", null)).StatusCode);

        // Сделка без валюты в запросе — в валюте каталога (USD), по цене каталога.
        var apt = await factory.WithDbAsync(db => db.Apartments.AsNoTracking().FirstAsync(a => a.Floor.BuildingId == b.Id && a.Status == ApartmentStatus.Free));
        var manager = await factory.CreateManagerClientAsync();
        var customer = await TestScenarios.ReadAsync<CustomerResponse>(await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Покупатель блока C", SitoraApiFactory.RandomPhone(), null, null, null)));
        var deal = await TestScenarios.ReadAsync<DealResponse>(await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apt.Id, customer.Id, DealStage.Lead, null, null, null), TestJson.Options));
        Assert.Equal(Currency.USD, deal.Currency);
        Assert.Equal(apt.Price, deal.Price);
    }
}

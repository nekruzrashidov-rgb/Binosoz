using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Формула ориентировочных расходов: площадь × кВт·ч/м² в год × тариф; округление до дирамов (половина — от нуля).
public class UtilityCalculatorTests
{
    [Fact]
    public void Year_Month_AndCost()
    {
        var e = UtilityCalculator.Calculate(area: 68.5m, kwhPerM2Year: 110m, tariffTjs: 0.29m, baselineKwhPerM2Year: null);
        Assert.Equal(7535.0m, e.KwhPerYear);          // 68.5 × 110
        Assert.Equal(627.9m, e.KwhPerMonth);           // 7535 / 12 = 627.916…
        Assert.Equal(2185.15m, e.CostPerYearTjs);      // 7535 × 0.29
        Assert.Equal(182.10m, e.CostPerMonthTjs);      // 2185.15 / 12 = 182.0958…
        Assert.Null(e.BaselineKwhPerYear);
        Assert.Null(e.SavingCostPerYearTjs);
    }

    [Fact]
    public void Baseline_Saving_IsBaselineMinusThisBuilding()
    {
        var e = UtilityCalculator.Calculate(50m, 100m, 0.25m, baselineKwhPerM2Year: 160m);
        Assert.Equal(5000m, e.KwhPerYear);
        Assert.Equal(8000m, e.BaselineKwhPerYear);
        Assert.Equal(3000m, e.SavingKwhPerYear);
        Assert.Equal(1250.00m, e.CostPerYearTjs);
        Assert.Equal(2000.00m, e.BaselineCostPerYearTjs);
        Assert.Equal(750.00m, e.SavingCostPerYearTjs);
    }

    [Fact]
    public void Rounding_HalfAwayFromZero()
    {
        // 1 × 1 × 0.005 = 0.005 → 0.01 (до дирамов, половина — вверх по модулю).
        Assert.Equal(0.01m, UtilityCalculator.Calculate(1m, 1m, 0.005m, null).CostPerYearTjs);
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UtilityCalculator.Calculate(0m, 100m, 0.3m, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => UtilityCalculator.Calculate(50m, -1m, 0.3m, null));
    }
}

// Тариф и коэффициенты задаёт руководитель организации; без тарифа или расчётного потребления дома расчёта нет (204).
public class GreenSettingsTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private async Task<(Guid BuildingId, Guid ApartmentId, decimal Area)> FirstApartmentAsync()
    {
        var guest = factory.CreateClient();
        var b = (await guest.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options))!
            .Items.OrderBy(x => x.Name).First(x => x.TotalApartments > 0);
        var apt = await factory.WithDbAsync(db => db.Apartments.Where(a => a.Floor.BuildingId == b.Id).OrderBy(a => a.Number).Select(a => new { a.Id, a.Area }).FirstAsync());
        return (b.Id, apt.Id, apt.Area);
    }

    [Fact]
    public async Task Utility_NeedsTariffAndBuildingEnergyUse_ThenComputes()
    {
        var (buildingId, aptId, area) = await FirstApartmentAsync();
        var guest = factory.CreateClient();
        var admin = await factory.CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await guest.GetAsync($"/api/v1/apartments/{aptId}/utility")).StatusCode);

        // Только тариф — у дома нет расчётного потребления: всё ещё 204.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/owner/green-settings", new { tariffKwhTjs = 0.29m })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await guest.GetAsync($"/api/v1/apartments/{aptId}/utility")).StatusCode);

        var content = await factory.CreateContentClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync($"/api/v1/buildings/{buildingId}/eco", new { estimatedKwhPerM2Year = 110m })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/owner/green-settings",
            new { tariffKwhTjs = 0.29m, baselineKwhPerM2Year = 160m, baselineSource = "Тестовый источник", isDemo = true })).StatusCode);

        var r = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/apartments/{aptId}/utility");
        var expected = UtilityCalculator.Calculate(area, 110m, 0.29m, 160m);
        Assert.Equal(expected.CostPerYearTjs, r.GetProperty("costPerYearTjs").GetDecimal());
        Assert.Equal(expected.SavingKwhPerYear, r.GetProperty("savingKwhPerYear").GetDecimal());
        Assert.Equal("Тестовый источник", r.GetProperty("baselineSource").GetString());
        Assert.True(r.GetProperty("isDemo").GetBoolean());

        // Признак демо — и в доме каталога (кэш сброшен при сохранении настроек).
        var b = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{buildingId}");
        Assert.True(b.GetProperty("isDemo").GetBoolean());
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Action == "GreenSettingsUpdated")));
    }

    [Fact]
    public async Task Settings_OnlyOrganizationLeaders_AndSourcesRequired()
    {
        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/owner/green-settings")).StatusCode);
        var content = await factory.CreateContentClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await content.PutAsJsonAsync("/api/v1/owner/green-settings", new { tariffKwhTjs = 0.3m })).StatusCode);

        var admin = await factory.CreateAdminClientAsync();
        var noSource = await admin.PutAsJsonAsync("/api/v1/owner/green-settings", new { co2KgPerKgPaper = 1.1m });
        Assert.True(noSource.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity);
        Assert.True((await noSource.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("co2Source", out _));

        var negative = await admin.PutAsJsonAsync("/api/v1/owner/green-settings", new { tariffKwhTjs = -1m });
        Assert.True(negative.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UnknownApartment_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/v1/apartments/{Guid.NewGuid()}/utility")).StatusCode);
    }
}

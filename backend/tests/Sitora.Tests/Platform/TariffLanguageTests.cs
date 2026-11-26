using System.Net.Http.Json;
using System.Text.Json;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Platform;

// C-07: тариф в CRM → «Настройки» — тот же, что на платформе → «Застройщики» (действующий OrganizationTariff),
// без тарифа — «не назначен», а не выдуманный «Стандарт». R-03: названия и возможности тарифов на трёх языках.
// R-07: версия в «Настройках платформы» — фактическая сборка, как /health/live.
public class TariffLanguageTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task OwnTariff_IsTheSameAsOnThePlatform_AndEmptyWithoutTariff()
    {
        var owner = await factory.CreateAdminClientAsync();
        var none = (await owner.GetFromJsonAsync<OwnTariffResponse>("/api/v1/owner/tariff", TestJson.Options))!;
        Assert.Null(none.Name);
        Assert.Null(none.MaxBuildings);
        Assert.True(none.Buildings >= 1);

        var platform = await factory.CreateSuperAdminClientAsync();
        var basic = (await platform.GetFromJsonAsync<List<TariffPlanResponse>>("/api/v1/platform/tariffs", TestJson.Options))!.Single(p => p.Name == "Basic");
        (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{Organization.DefaultId}/tariff", new AssignTariffRequest(basic.Id), TestJson.Options))
            .EnsureSuccessStatusCode();

        var own = (await owner.GetFromJsonAsync<OwnTariffResponse>("/api/v1/owner/tariff", TestJson.Options))!;
        Assert.Equal(("Basic", (int?)3, (int?)20), (own.Name, own.MaxBuildings, own.MaxUsers));
        var listed = (await platform.GetFromJsonAsync<List<OrganizationResponse>>("/api/v1/platform/organizations", TestJson.Options))!
            .Single(o => o.Id == Organization.DefaultId);
        Assert.Equal(own.Name, listed.Tariff?.TariffName);

        // На таджикском — переведённое название в обоих местах.
        owner.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tg");
        Assert.Equal("Асосӣ", (await owner.GetFromJsonAsync<OwnTariffResponse>("/api/v1/owner/tariff", TestJson.Options))!.Name);

        // Покупателю и гостю тариф организации не отдаётся.
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/owner/tariff")).StatusCode);
    }

    [Theory]
    [InlineData("ru", "Free", "CRM, каталог и кабинет покупателя")]
    [InlineData("tg", "Ройгон", "CRM, каталог ва кабинети харидор")]
    [InlineData("en", "Free", "CRM, catalog and buyer cabinet")]
    public async Task SeededTariffs_InTheLanguageOfTheRequest(string language, string name, string feature)
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        platform.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var free = (await platform.GetFromJsonAsync<List<TariffPlanResponse>>("/api/v1/platform/tariffs", TestJson.Options))!.Single(p => p.Name == "Free");
        Assert.Equal(name, free.LocalName);
        Assert.Equal([feature], free.LocalFeatures);
        // Форма правит все три языка: переводы приходят всегда.
        Assert.Equal("Ройгон", free.NameTg);
        Assert.Equal(["CRM, catalog and buyer cabinet"], free.FeaturesEn);
    }

    [Fact]
    public async Task SavedTranslations_AndFallbackToRussian()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var name = "Язык-" + Guid.NewGuid().ToString("N")[..6];
        var created = await TestScenarios.ReadAsync<TariffPlanResponse>(await platform.PostAsJsonAsync("/api/v1/platform/tariffs",
            new SaveTariffRequest(name, 100m, Currency.TJS, null, null, ["Отчёты"], true, "Тоҷикӣ", null, ["Ҳисобот"], null), TestJson.Options));
        Assert.Equal("Тоҷикӣ", created.NameTg);

        platform.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        var en = (await platform.GetFromJsonAsync<List<TariffPlanResponse>>("/api/v1/platform/tariffs", TestJson.Options))!.Single(p => p.Id == created.Id);
        // Английского перевода нет — русский текст.
        Assert.Equal(name, en.LocalName);
        Assert.Equal(["Отчёты"], en.LocalFeatures!);
    }

    [Fact]
    public async Task PlatformSettings_Version_IsTheBuildFromHealthLive()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var settings = (await platform.GetFromJsonAsync<PlatformSettingsResponse>("/api/v1/platform/settings", TestJson.Options))!;
        using var live = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/health/live"));
        Assert.Equal(live.RootElement.GetProperty("version").GetString(), settings.Version);
        Assert.Equal(live.RootElement.GetProperty("builtAt").GetString(), settings.BuiltAt);
    }
}

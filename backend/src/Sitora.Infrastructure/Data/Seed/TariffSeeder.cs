using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;

namespace Sitora.Infrastructure.Data.Seed;

// Базовые тарифы платформы в любом окружении: создаются, только если тарифа с таким названием ещё нет
// (изменённые суперадмином цены и лимиты не перезаписываются). Переводы (R-03) заполняются и у уже созданных, но только
// пустые и только если русские возможности остались как у сидера — правки суперадмина не трогаются.
public static class TariffSeeder
{
    public static readonly IReadOnlyDictionary<string, (string NameTg, string NameEn, string[] FeaturesTg, string[] FeaturesEn)> Translations =
        new Dictionary<string, (string, string, string[], string[])>
        {
            ["Free"] = ("Ройгон", "Free", ["CRM, каталог ва кабинети харидор"], ["CRM, catalog and buyer cabinet"]),
            ["Basic"] = ("Асосӣ", "Basic", ["Ҳамаи имкониятҳои Free", "Ҳисоботҳо барои соҳиб", "Огоҳиномаҳо дар Telegram"],
                ["Everything in Free", "Reports for the owner", "Telegram notifications"]),
            ["Pro"] = ("Pro", "Pro", ["Ҳамаи имкониятҳои Basic", "Ёрдамчии AI", "Домени худ"],
                ["Everything in Basic", "AI assistant", "Own domain"])
        };

    public static readonly (string Name, decimal Price, int? MaxBuildings, int? MaxUsers, string[] Features)[] Plans =
    [
        // Лимиты домов и сотрудников видны отдельно; здесь — только возможности.
        ("Free", 0m, 1, 5, ["CRM, каталог и кабинет покупателя"]),
        ("Basic", 500m, 3, 20, ["Всё из Free", "Отчёты владельцу", "Telegram-уведомления"]),
        ("Pro", 1500m, null, null, ["Всё из Basic", "AI-помощник", "Свой домен"])
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        var existing = await db.TariffPlans.ToListAsync();
        foreach (var (name, price, buildings, users, features) in Plans.Where(p => existing.All(e => e.Name != p.Name)))
        {
            var (nameTg, nameEn, featuresTg, featuresEn) = Translations[name];
            db.TariffPlans.Add(new TariffPlan
            {
                Name = name,
                NameTg = nameTg,
                NameEn = nameEn,
                Price = price,
                Currency = Currency.TJS,
                MaxBuildings = buildings,
                MaxUsers = users,
                Features = JsonSerializer.Serialize(features),
                FeaturesTg = JsonSerializer.Serialize(featuresTg),
                FeaturesEn = JsonSerializer.Serialize(featuresEn)
            });
        }

        foreach (var plan in existing)
        {
            var seeded = Plans.FirstOrDefault(p => p.Name == plan.Name);
            if (seeded.Name is null || !Translations.TryGetValue(plan.Name, out var tr))
                continue;
            var sameFeatures = TariffServiceFeatures(plan.Features).SequenceEqual(seeded.Features);
            plan.NameTg ??= tr.NameTg;
            plan.NameEn ??= tr.NameEn;
            if (sameFeatures && TariffServiceFeatures(plan.FeaturesTg).Count == 0)
                plan.FeaturesTg = JsonSerializer.Serialize(tr.FeaturesTg);
            if (sameFeatures && TariffServiceFeatures(plan.FeaturesEn).Count == 0)
                plan.FeaturesEn = JsonSerializer.Serialize(tr.FeaturesEn);
        }
        await db.SaveChangesAsync();
    }

    private static IReadOnlyList<string> TariffServiceFeatures(string json) => Services.TariffService.ParseFeatures(json);
}

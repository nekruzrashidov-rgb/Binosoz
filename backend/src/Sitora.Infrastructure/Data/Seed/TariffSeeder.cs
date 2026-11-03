using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;

namespace Sitora.Infrastructure.Data.Seed;

// Базовые тарифы платформы в любом окружении: создаются, только если тарифа с таким названием ещё нет
// (изменённые суперадмином цены и лимиты не перезаписываются).
public static class TariffSeeder
{
    public static readonly (string Name, decimal Price, int? MaxBuildings, int? MaxUsers, string[] Features)[] Plans =
    [
        ("Free", 0m, 1, 5, ["1 дом", "До 5 сотрудников", "CRM и кабинет покупателя"]),
        ("Basic", 500m, 3, 20, ["До 3 домов", "До 20 сотрудников", "Отчёты и Telegram-уведомления"]),
        ("Pro", 1500m, null, null, ["Без ограничения домов", "Без ограничения сотрудников", "AI-помощник и свой домен"])
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        var existing = await db.TariffPlans.Select(t => t.Name).ToListAsync();
        foreach (var (name, price, buildings, users, features) in Plans.Where(p => !existing.Contains(p.Name)))
            db.TariffPlans.Add(new TariffPlan
            {
                Name = name,
                Price = price,
                Currency = Currency.TJS,
                MaxBuildings = buildings,
                MaxUsers = users,
                Features = JsonSerializer.Serialize(features)
            });
        await db.SaveChangesAsync();
    }
}

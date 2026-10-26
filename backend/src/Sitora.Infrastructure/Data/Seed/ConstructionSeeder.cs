using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Seed;

// Этапы стройки «Sitora Residence» с примерными процентами для разработки.
public static class ConstructionSeeder
{
    public static readonly (string Name, int Percent)[] Stages =
    [
        ("Котлован", 100),
        ("Фундамент", 100),
        ("Монолитный каркас", 65),
        ("Кладка стен", 40),
        ("Кровля", 0),
        ("Инженерные сети", 15),
        ("Фасад", 0),
        ("Отделка мест общего пользования", 0)
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        var buildingId = await db.Buildings.Where(b => b.NameRu == CatalogSeeder.BuildingName).Select(b => (Guid?)b.Id).FirstOrDefaultAsync();
        if (buildingId is null || await db.ConstructionStages.AnyAsync(s => s.BuildingId == buildingId))
            return;

        db.ConstructionStages.AddRange(Stages.Select((s, i) => new ConstructionStage
        {
            BuildingId = buildingId.Value,
            Name = s.Name,
            Percent = s.Percent,
            SortOrder = (i + 1) * 10
        }));
        await db.SaveChangesAsync();
    }
}

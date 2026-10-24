using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sitora.Infrastructure.Data;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Фабрика только ради временной базы: сервер API здесь не запускается.
public class EmptyDatabaseFactory : SitoraApiFactory;

// Миграции BuildingDescriptionTg и ApartmentPlanSlot исправлены вручную — проверяем их на уже существующих данных.
public class MigrationDataTests(EmptyDatabaseFactory factory) : IClassFixture<EmptyDatabaseFactory>
{
    private const string LastMigrationBeforeChanges = "20261024131457_ConstructionAndChat";

    [Fact]
    public async Task HandEditedMigrations_KeepExistingData()
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(factory.ConnectionString).Options, TimeProvider.System);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(LastMigrationBeforeChanges);

        var buildingId = Guid.NewGuid();
        var floorId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Buildings" ("Id", "NameRu", "NameTg", "Address", "FloorsCount", "Description", "CreatedAt", "UpdatedAt")
            VALUES ({buildingId}, 'Старый дом', 'Старый дом', 'г. Душанбе', 5, 'Старое описание по-русски', now(), now())
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Floors" ("Id", "BuildingId", "Number", "CreatedAt", "UpdatedAt")
            VALUES ({floorId}, {buildingId}, 1, now(), now())
            """);
        foreach (var number in new[] { 103, 101, 102 })
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO "Apartments" ("Id", "FloorId", "Number", "Rooms", "Area", "PricePerM2", "Price", "Currency", "Status", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {floorId}, {number}, 1, 40, 9000, 360000, 'TJS', 'Free', now(), now())
                """);
        }

        await migrator.MigrateAsync();

        var building = await db.Buildings.AsNoTracking().SingleAsync(b => b.Id == buildingId);
        Assert.Equal("Старое описание по-русски", building.DescriptionRu);
        Assert.Null(building.DescriptionTg);

        var slots = await db.Apartments.AsNoTracking()
            .Where(a => a.FloorId == floorId)
            .OrderBy(a => a.Number)
            .Select(a => a.PlanSlot)
            .ToListAsync();
        Assert.Equal([0, 1, 2], slots);
    }
}

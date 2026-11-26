using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Sitora.Infrastructure.Data;

namespace Sitora.Tests.Catalog;

// Миграция ApartmentBlockNumbering на базе с прежним домом «Некруз Резиденс» (как на проде): номера не меняются,
// дом квартиры заполняется по этажу; повтор номера в доме — понятная ошибка с номерами, данные не тронуты.
internal static class OldHouseDb
{
    public const string Before = "20261126144227_BuildingPublished";

    public static AppDbContext Open(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options, TimeProvider.System);

    // Прежний дом: этажи 1–2, квартиры floor·100 + n; duplicate — ещё одна кв. №101 на 2-м этаже (раньше номер был
    // уникален только на этаже).
    public static async Task<(Guid Building, Guid Floor1, Guid Floor2)> SeedAsync(AppDbContext db, bool duplicate)
    {
        var org = await db.Database.SqlQueryRaw<Guid>("""SELECT "Id" AS "Value" FROM "Organizations" LIMIT 1""").ToListAsync();
        var building = Guid.NewGuid();
        var orgId = org.Count > 0 ? (Guid?)org[0] : null;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Buildings" ("Id", "OrganizationId", "NameRu", "NameTg", "Address", "FloorsCount", "CreatedAt", "UpdatedAt")
            VALUES ({building}, {orgId}, 'Некруз Резиденс', 'Некруз Резиденс', 'г. Душанбе', 2, now(), now())
            """);
        var floors = new List<Guid>();
        foreach (var n in new[] { 1, 2 })
        {
            var floor = Guid.NewGuid(); floors.Add(floor);
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO "Floors" ("Id", "BuildingId", "Number", "CreatedAt", "UpdatedAt") VALUES ({floor}, {building}, {n}, now(), now())
                """);
            for (var slot = 0; slot < 3; slot++)
                await InsertAsync(db, floor, n * 100 + slot + 1, slot);
        }
        if (duplicate)
            await InsertAsync(db, floors[1], 101, 5);
        return (building, floors[0], floors[1]);
    }

    private static Task InsertAsync(AppDbContext db, Guid floor, int number, int slot) => db.Database.ExecuteSqlAsync($"""
        INSERT INTO "Apartments" ("Id", "FloorId", "Number", "PlanSlot", "Rooms", "Area", "PricePerM2", "Price", "Currency", "Status", "CreatedAt", "UpdatedAt")
        VALUES ({Guid.NewGuid()}, {floor}, {number}, {slot}, 2, 60, 7000, 420000, 'TJS', 'Sold', now(), now())
        """);
}

public class BlockNumberingMigrationTests(EmptyDatabaseFactory factory) : IClassFixture<EmptyDatabaseFactory>
{
    [Fact]
    public async Task OldHouse_NumbersKept_BuildingIdFromFloor()
    {
        await using var db = OldHouseDb.Open(factory.ConnectionString);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(OldHouseDb.Before);
        var (building, _, _) = await OldHouseDb.SeedAsync(db, duplicate: false);

        await migrator.MigrateAsync();

        var rows = await db.Apartments.AsNoTracking().Where(a => a.Floor.BuildingId == building).OrderBy(a => a.Number)
            .Select(a => new { a.Number, a.BuildingId }).ToListAsync();
        Assert.Equal([101, 102, 103, 201, 202, 203], rows.Select(r => r.Number));
        Assert.All(rows, r => Assert.Equal(building, r.BuildingId));
    }
}

public class BlockNumberingMigrationDuplicateTests(EmptyDatabaseFactory factory) : IClassFixture<EmptyDatabaseFactory>
{
    [Fact]
    public async Task DuplicateNumberInOldHouse_StopsWithNumbers_DataUntouched()
    {
        await using var db = OldHouseDb.Open(factory.ConnectionString);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(OldHouseDb.Before);
        var (building, _, _) = await OldHouseDb.SeedAsync(db, duplicate: true);

        var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains("Номера помещений повторяются в пределах дома", error.MessageText);
        Assert.Contains("Некруз Резиденс: №101", error.MessageText);

        // Миграция откатилась целиком: колонки BuildingId нет, номера те же, применённой миграции нет.
        var column = await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM information_schema.columns WHERE table_name = 'Apartments' AND column_name = 'BuildingId'
            """).SingleAsync();
        Assert.Equal(0, column);
        var numbers = await db.Database.SqlQuery<int>($"""
            SELECT a."Number" AS "Value" FROM "Apartments" a JOIN "Floors" f ON f."Id" = a."FloorId" WHERE f."BuildingId" = {building} ORDER BY 1
            """).ToListAsync();
        Assert.Equal([101, 101, 102, 103, 201, 202, 203], numbers);
        Assert.DoesNotContain("20261126161005_ApartmentBlockNumbering", await db.Database.GetAppliedMigrationsAsync());
    }
}

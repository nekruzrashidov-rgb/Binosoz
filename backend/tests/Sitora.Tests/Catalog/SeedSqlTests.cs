using Npgsql;
using Sitora.Domain.Entities;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// scripts/seed.sql — заполнение пустой продакшен-базы (Render). Скрипт выполняется на базе этого класса тестов
// в транзакции и откатывается: дом из сидера разработки на время переименовывается, чтобы база была «пустой» для скрипта.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "Тест: SQL — константы и файл scripts/seed.sql из репозитория, id — из этой же базы; ввода пользователя нет.")]
public class SeedSqlTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task SeedSql_Creates20FloorsAnd160Apartments_WithStatusesPricesAndBookings_AndIsIdempotent()
    {
        factory.CreateClient(); // база создаётся и мигрирует при запуске сервера
        var sql = await File.ReadAllTextAsync(FindSeedSql());

        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await ExecAsync(connection, tx, "UPDATE \"Buildings\" SET \"NameRu\" = 'Некруз Резиденс (сидер)' WHERE \"NameRu\" = 'Некруз Резиденс'");

        await ExecAsync(connection, tx, sql);

        var buildingId = await ScalarAsync<Guid>(connection, tx,
            $"SELECT \"Id\" FROM \"Buildings\" WHERE \"NameRu\" = 'Некруз Резиденс' AND \"OrganizationId\" = '{Organization.DefaultId}'");
        Assert.Equal(20, await ScalarAsync<int>(connection, tx, $"SELECT \"FloorsCount\" FROM \"Buildings\" WHERE \"Id\" = '{buildingId}'"));
        Assert.Equal(21L, await ScalarAsync<long>(connection, tx, $"SELECT count(*) FROM \"Floors\" WHERE \"BuildingId\" = '{buildingId}'"));

        const string apartments = "FROM \"Apartments\" a JOIN \"Floors\" f ON f.\"Id\" = a.\"FloorId\" WHERE f.\"BuildingId\" = '{0}'";
        string Where(string extra) => string.Format(apartments, buildingId) + extra;
        Assert.Equal(160L, await ScalarAsync<long>(connection, tx, "SELECT count(*) " + Where("")));
        Assert.Equal(0L, await ScalarAsync<long>(connection, tx, "SELECT count(*) " + Where(" AND f.\"Number\" = 0")));
        Assert.Equal(6L, await ScalarAsync<long>(connection, tx, "SELECT count(*) " + Where(" AND a.\"Status\" = 'Sold'")));
        Assert.Equal(5L, await ScalarAsync<long>(connection, tx, "SELECT count(*) " + Where(" AND a.\"Status\" = 'Booked'")));
        Assert.Equal(149L, await ScalarAsync<long>(connection, tx, "SELECT count(*) " + Where(" AND a.\"Status\" = 'Free'")));
        Assert.Equal("Sold", await ScalarAsync<string>(connection, tx, "SELECT a.\"Status\" " + Where(" AND a.\"Number\" = 302")));
        Assert.Equal("Booked", await ScalarAsync<string>(connection, tx, "SELECT a.\"Status\" " + Where(" AND a.\"Number\" = 503")));
        Assert.Equal("Free", await ScalarAsync<string>(connection, tx, "SELECT a.\"Status\" " + Where(" AND a.\"Number\" = 603")));

        // Цены — как Apartment.CalculatePrice для каждого типа.
        foreach (var (slot, rooms, area, ppm) in new[] { (0, 1, 42.5m, 7500m), (3, 2, 68.0m, 7000m), (4, 3, 92.0m, 6800m), (7, 4, 118.5m, 6500m) })
        {
            var row = await RowAsync(connection, tx, "SELECT a.\"Rooms\", a.\"Area\", a.\"PricePerM2\", a.\"Price\", a.\"Currency\" " +
                Where($" AND f.\"Number\" = 10 AND a.\"PlanSlot\" = {slot}"));
            Assert.Equal((rooms, area, ppm, Apartment.CalculatePrice(area, ppm), "TJS"),
                ((int)row[0], (decimal)row[1], (decimal)row[2], (decimal)row[3], (string)row[4]));
        }

        // У каждой брони — активная бронь с демо-заявкой, у проданных и свободных броней нет.
        Assert.Equal(5L, await ScalarAsync<long>(connection, tx,
            "SELECT count(*) FROM \"Bookings\" b JOIN \"Apartments\" a ON a.\"Id\" = b.\"ApartmentId\" JOIN \"Floors\" f ON f.\"Id\" = a.\"FloorId\" " +
            $"JOIN \"Leads\" l ON l.\"Id\" = b.\"LeadId\" WHERE f.\"BuildingId\" = '{buildingId}' AND b.\"Status\" = 'Active' AND a.\"Status\" = 'Booked' AND l.\"OrganizationId\" = '{Organization.DefaultId}'"));
        Assert.Equal(1L, await ScalarAsync<long>(connection, tx,
            $"SELECT count(*) FROM \"AuditLog\" WHERE \"EntityId\" = '{buildingId}' AND \"Action\" = 'Seeded'"));

        // Повторный запуск ничего не добавляет.
        await ExecAsync(connection, tx, sql);
        Assert.Equal(1L, await ScalarAsync<long>(connection, tx,
            $"SELECT count(*) FROM \"Buildings\" WHERE \"NameRu\" = 'Некруз Резиденс' AND \"OrganizationId\" = '{Organization.DefaultId}'"));

        await tx.RollbackAsync();
    }

    private static string FindSeedSql()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "scripts", "seed.sql");
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException("Не найден scripts/seed.sql");
    }

    private static async Task ExecAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, tx);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, NpgsqlTransaction tx, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, tx);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<object[]> RowAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, tx);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var values = new object[reader.FieldCount];
        reader.GetValues(values);
        return values;
    }
}

using Npgsql;
using Sitora.Domain.Entities;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// backend/scripts/seed.sql — заполнение пустой продакшен-базы (Render). Скрипт выполняется на базе этого класса тестов
// в транзакции и откатывается: дом из сидера разработки на время переименовывается, чтобы база была «пустой» для скрипта.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "Тест: SQL — константы и файл backend/scripts/seed.sql из репозитория, id — из этой же базы; ввода пользователя нет.")]
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

        // Демо-сделки «Зелёного эффекта» добавляются, если их ещё нет; уже выданные настоящие PDF (акт сверки
        // демо-покупателя) этому не мешают — новые PDF считаются сверх них.
        var demoDealsExpected = await ScalarAsync<long>(connection, tx,
            "SELECT count(*) FROM \"Deals\" d JOIN \"Apartments\" a ON a.\"Id\" = d.\"ApartmentId\" JOIN \"Floors\" f ON f.\"Id\" = a.\"FloorId\" " +
            $"JOIN \"Buildings\" b ON b.\"Id\" = f.\"BuildingId\" WHERE b.\"OrganizationId\" = '{Organization.DefaultId}' AND d.\"StageReason\" = 'Демо-данные'") == 0;
        var pdfsBefore = await ScalarAsync<long>(connection, tx, $"SELECT count(*) FROM \"IssuedPdfs\" WHERE \"OrganizationId\" = '{Organization.DefaultId}'");
        var pagesBefore = await ScalarAsync<long>(connection, tx, $"SELECT coalesce(sum(\"PageCount\"), 0) FROM \"IssuedPdfs\" WHERE \"OrganizationId\" = '{Organization.DefaultId}'");

        await ExecAsync(connection, tx, sql);

        var buildingId = await ScalarAsync<Guid>(connection, tx,
            $"SELECT \"Id\" FROM \"Buildings\" WHERE \"NameRu\" = 'Некруз Резиденс' AND \"OrganizationId\" = '{Organization.DefaultId}'");
        Assert.Equal(20, await ScalarAsync<int>(connection, tx, $"SELECT \"FloorsCount\" FROM \"Buildings\" WHERE \"Id\" = '{buildingId}'"));
        Assert.Equal(21L, await ScalarAsync<long>(connection, tx, $"SELECT count(*) FROM \"Floors\" WHERE \"BuildingId\" = '{buildingId}'"));
        // Описание — без непроверенных оценок («бизнес-класс») и с переводом на таджикский.
        var description = await RowAsync(connection, tx, $"SELECT \"DescriptionRu\", \"DescriptionTg\" FROM \"Buildings\" WHERE \"Id\" = '{buildingId}'");
        Assert.DoesNotContain("бизнес", (string)description[0], StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(description[1] as string));


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

        // Уже засеянная база со старым описанием «бизнес-класса»: повторный запуск заменяет его и заполняет перевод.
        await ExecAsync(connection, tx, $"UPDATE \"Buildings\" SET \"DescriptionRu\" = 'Жилой комплекс бизнес-класса с коммерческим первым этажом', \"DescriptionTg\" = NULL WHERE \"Id\" = '{buildingId}'");
        await ExecAsync(connection, tx, sql);
        var fixedDescription = await RowAsync(connection, tx, $"SELECT \"DescriptionRu\", \"DescriptionTg\" FROM \"Buildings\" WHERE \"Id\" = '{buildingId}'");
        Assert.Equal(description[0], fixedDescription[0]);
        Assert.Equal(description[1], fixedDescription[1]);

        if (demoDealsExpected && await ScalarAsync<bool>(connection, tx, $"SELECT \"GreenIsDemo\" FROM \"Organizations\" WHERE \"Id\" = '{Organization.DefaultId}'"))
        {
            // 6 проданных квартир — 6 завершённых демо-сделок, по 5 выданных PDF (8 страниц) на каждую.
            Assert.Equal(6L, await ScalarAsync<long>(connection, tx, "SELECT count(*) FROM \"Deals\" d JOIN \"Apartments\" a ON a.\"Id\" = d.\"ApartmentId\" JOIN \"Floors\" f ON f.\"Id\" = a.\"FloorId\" " +
                $"WHERE f.\"BuildingId\" = '{buildingId}' AND a.\"Status\" = 'Sold' AND d.\"Stage\" = 'Completed'"));
            Assert.Equal(pdfsBefore + 30L, await ScalarAsync<long>(connection, tx, $"SELECT count(*) FROM \"IssuedPdfs\" WHERE \"OrganizationId\" = '{Organization.DefaultId}'"));
            Assert.Equal(pagesBefore + 48L, await ScalarAsync<long>(connection, tx, $"SELECT coalesce(sum(\"PageCount\"), 0) FROM \"IssuedPdfs\" WHERE \"OrganizationId\" = '{Organization.DefaultId}'"));
        }

        // C-05: демо-бронь держится 48 часов, как обещает сайт, и каждый запуск снова продлевает действующие демо-брони
        // до 48 часов (в воронке «47 ч», а не «20 дн»).
        const string demoBookings = "FROM \"Bookings\" b JOIN \"Leads\" l ON l.\"Id\" = b.\"LeadId\" " +
            "WHERE l.\"Comment\" LIKE 'Демо-данные: бронь квартиры %' AND b.\"Status\" = 'Active'";
        Assert.True(await ScalarAsync<bool>(connection, tx,
            $"SELECT bool_and(b.\"ExpiresAt\" > now() + interval '47 hours' AND b.\"ExpiresAt\" <= now() + interval '48 hours') {demoBookings}"));
        await ExecAsync(connection, tx, $"UPDATE \"Bookings\" SET \"ExpiresAt\" = now() + interval '30 days' WHERE \"Id\" IN (SELECT b.\"Id\" {demoBookings})");
        await ExecAsync(connection, tx, sql);
        Assert.True(await ScalarAsync<bool>(connection, tx, $"SELECT bool_and(b.\"ExpiresAt\" <= now() + interval '48 hours') {demoBookings}"));
        Assert.True(await ScalarAsync<long>(connection, tx, "SELECT count(*) FROM \"AuditLog\" WHERE \"Action\" = 'DemoBookingsRenewed'") >= 1);

        // R-01: старое имя организации «Sitora» меняется на «Некруз Резиденс»; имя, заданное владельцем, не трогается.
        await ExecAsync(connection, tx, $"UPDATE \"Organizations\" SET \"Name\" = 'Sitora' WHERE \"Id\" = '{Organization.DefaultId}'");
        await ExecAsync(connection, tx, sql);
        Assert.Equal("Некруз Резиденс", await ScalarAsync<string>(connection, tx, $"SELECT \"Name\" FROM \"Organizations\" WHERE \"Id\" = '{Organization.DefaultId}'"));
        await ExecAsync(connection, tx, $"UPDATE \"Organizations\" SET \"Name\" = 'ЖК Свой' WHERE \"Id\" = '{Organization.DefaultId}'");
        await ExecAsync(connection, tx, sql);
        Assert.Equal("ЖК Свой", await ScalarAsync<string>(connection, tx, $"SELECT \"Name\" FROM \"Organizations\" WHERE \"Id\" = '{Organization.DefaultId}'"));

        // Повторный запуск ничего не добавляет.
        var issuedBefore = await ScalarAsync<long>(connection, tx, "SELECT count(*) FROM \"IssuedPdfs\"");
        await ExecAsync(connection, tx, sql);
        Assert.Equal(issuedBefore, await ScalarAsync<long>(connection, tx, "SELECT count(*) FROM \"IssuedPdfs\""));
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
        throw new FileNotFoundException("Не найден backend/scripts/seed.sql");
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

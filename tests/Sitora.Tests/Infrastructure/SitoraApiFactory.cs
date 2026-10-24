using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Sitora.Tests.Infrastructure;

// По умолчанию поднимает PostgreSQL 18 в Testcontainers. Если Docker недоступен,
// задайте SITORA_TEST_DB со строкой подключения к своему серверу: для каждого
// прогона создаётся отдельная временная база, после тестов она удаляется.
public class SitoraApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@sitora.local";
    public const string ManagerEmail = "manager@sitora.local";

    // Тестовые пароли генерируются на каждый прогон и нигде не хранятся.
    public string AdminPassword { get; } = GeneratePassword();
    public string ManagerPassword { get; } = GeneratePassword();

    private readonly string _jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private PostgreSqlContainer? _container;
    private string? _serverConnectionString;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("SITORA_TEST_DB");
        if (!string.IsNullOrWhiteSpace(external))
        {
            _serverConnectionString = external;
            _connectionString = new NpgsqlConnectionStringBuilder(external)
            {
                Database = $"sitora_test_{Guid.NewGuid():N}"
            }.ConnectionString;
            return;
        }

        _container = new PostgreSqlBuilder("postgres:18").Build();
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", _jwtKey);
        builder.UseSetting("SEED_ADMIN_PASSWORD", AdminPassword);
        builder.UseSetting("SEED_MANAGER_PASSWORD", ManagerPassword);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        if (_container is not null)
            await _container.DisposeAsync();

        if (_serverConnectionString is not null)
        {
            var database = new NpgsqlConnectionStringBuilder(_connectionString).Database;
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_serverConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string GeneratePassword() =>
        "Aa1-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
}

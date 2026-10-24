using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Sitora.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace Sitora.Tests.Infrastructure;

// По умолчанию поднимает PostgreSQL 18 в Testcontainers. Если Docker недоступен,
// задайте SITORA_TEST_DB со строкой подключения к своему серверу: для каждого
// прогона создаётся отдельная временная база, после тестов она удаляется.
public class SitoraApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@sitora.local";
    public const string ManagerEmail = "manager@sitora.local";
    public const string ContentEmail = "content@sitora.local";
    public const string AccountantEmail = "accountant@sitora.local";

    static SitoraApiFactory()
    {
        // Program читает этот флаг до сборки хоста, поэтому задаём его переменной окружения.
        // Фоновые задачи в тестах запускаются вручную.
        Environment.SetEnvironmentVariable("Hangfire__ServerEnabled", "false");
    }

    // Тестовые пароли и ключи генерируются на каждый прогон и нигде не хранятся.
    public string AdminPassword { get; } = GeneratePassword();
    public string ManagerPassword { get; } = GeneratePassword();
    public string ContentPassword { get; } = GeneratePassword();
    public string AccountantPassword { get; } = GeneratePassword();

    public Task<HttpClient> CreateAdminClientAsync() => CreateClientForAsync(AdminEmail, AdminPassword);
    public Task<HttpClient> CreateManagerClientAsync() => CreateClientForAsync(ManagerEmail, ManagerPassword);
    public Task<HttpClient> CreateContentClientAsync() => CreateClientForAsync(ContentEmail, ContentPassword);
    public Task<HttpClient> CreateAccountantClientAsync() => CreateClientForAsync(AccountantEmail, AccountantPassword);

    // Свободная квартира из сидера, на которую нет открытой сделки.
    public Task<Guid> FindFreeApartmentAsync() => WithDbAsync(db => db.Apartments
        .Where(a => a.Status == ApartmentStatus.Free
                    && !db.Deals.Any(d => d.ApartmentId == a.Id && d.Stage != DealStage.Cancelled))
        .OrderBy(a => a.Floor.Number).ThenBy(a => a.Number)
        .Select(a => a.Id)
        .FirstAsync());

    public static string RandomPhone() => "+99277" + Random.Shared.Next(1_000_000, 9_999_999);

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task WithServiceAsync<TService>(Func<TService, Task> action) where TService : notnull
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task<HttpClient> CreateClientForAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private readonly string _jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private readonly string _encryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
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
        builder.UseSetting("SEED_CONTENT_PASSWORD", ContentPassword);
        builder.UseSetting("SEED_ACCOUNTANT_PASSWORD", AccountantPassword);
        builder.UseSetting("Encryption:Key", _encryptionKey);
        builder.UseSetting("Storage:RootPath", _storagePath);
        builder.ConfigureTestServices(services => services.AddSingleton<ISmsSender>(Sms));
    }

    public FakeSmsSender Sms { get; } = new();

    // Входит как покупатель с этим телефоном через SMS-код.
    public async Task<HttpClient> CreateCustomerClientAsync(string phone)
    {
        var client = CreateClient();
        var sent = await client.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        sent.EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, Sms.LastCode(phone)!));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "sitora-tests", Guid.NewGuid().ToString("N"));

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        if (Directory.Exists(_storagePath))
            Directory.Delete(_storagePath, recursive: true);

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

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Sitora.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
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
    public const string AllowedOrigin = "https://sitora.test";

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
    public string SeedSuperAdminPassword { get; } = GeneratePassword();

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
        builder.UseSetting("SEED_SUPERADMIN_PASSWORD", SeedSuperAdminPassword);
        builder.UseSetting("Encryption:Key", _encryptionKey);
        builder.UseSetting("Storage:RootPath", _storagePath);
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        // Все запросы TestServer идут «с одного IP», поэтому лимиты для обычных тестов снимаем;
        // сами лимиты проверяет RateLimitTests со своей фабрикой.
        builder.UseSetting("RateLimiting:LoginPerMinute", "1000000");
        builder.UseSetting("RateLimiting:SmsPerHour", "1000000");
        builder.UseSetting("RateLimiting:PublicFormsPerHour", "1000000");
        builder.UseSetting("RateLimiting:ApplicationsPerHour", "1000000");
        builder.UseSetting("RateLimiting:AnonymousPerMinute", "1000000");
        builder.UseSetting("RateLimiting:UserPerMinute", "1000000");
        // Лимиты кодов из почты проверяет EmailAuthTests со своей фабрикой; здесь покупатели входят много раз подряд.
        builder.UseSetting("Auth:EmailCodesPerMinute", "100");
        builder.UseSetting("Auth:EmailCodesPerHour", "1000");
        // Старый вход по SMS выключен по умолчанию; его тесты проверяют сам вход, поэтому здесь он включён.
        builder.UseSetting("Seed:GreenDemo", "false");
        builder.UseSetting("Auth:SmsLoginEnabled", SmsLoginEnabled ? "true" : "false");
        // Второй застройщик из сидера разработки не нужен: изоляцию проверяют тесты со своими организациями.
        builder.UseSetting("Seed:RichTower", "false");
        // Код 123456 для любой почты (Development) выключен: тесты проверяют настоящие коды; режим проверяет DevFixedCodeTests.
        builder.UseSetting("Auth:DevFixedCodeForAnyEmail", "false");
        builder.UseSetting("ADMIN_EMAIL", SuperAdminEmail);
        builder.UseSetting("ADMIN_PASSWORD", SuperAdminPassword);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ISmsSender>(Sms);
            services.AddSingleton<IEmailSender>(Email);
            services.AddSingleton<IRateProvider>(Rates);
            services.AddSingleton<ITelegramNotifier>(Telegram);
        });
    }

    protected virtual bool SmsLoginEnabled => true;

    public const string SuperAdminEmail = "owner@platform.test";
    public string SuperAdminPassword { get; } = GeneratePassword();

    public FakeSmsSender Sms { get; } = new();
    public FakeRateProvider Rates { get; } = new();
    public FakeTelegramNotifier Telegram { get; } = new();
    public FakeEmailSender Email { get; } = new();

    public string ConnectionString => _connectionString;

    // Входит как покупатель с этим телефоном по коду из почты. Если у клиента нет почты, задаёт её.
    public async Task<HttpClient> CreateCustomerClientAsync(string phone)
    {
        var email = await WithDbAsync(async db =>
        {
            var customers = await db.Customers.Where(c => c.Phone == phone).ToListAsync();
            var existing = customers.Select(c => c.Email).FirstOrDefault(e => e != null);
            var address = existing ?? $"c{phone.TrimStart('+')}@customers.test";
            customers.ForEach(c => c.Email ??= address);
            await db.SaveChangesAsync();
            return address;
        });
        return await CreateEmailClientAsync(email);
    }

    // Вход покупателя по коду из почты.
    public async Task<HttpClient> CreateEmailClientAsync(string email)
    {
        var client = CreateClient();
        var sent = await client.PostAsJsonAsync("/api/v1/auth/email/send", new EmailSendRequest(email));
        sent.EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new EmailVerifyRequest(email, Email.LastCode(email)!));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    // Сотрудник, которого тест создал без известного пароля: задаём пароль напрямую и входим по нему, как на странице входа.
    public const string StaffTestPassword = "Staff-Test-1";

    public async Task<HttpClient> CreateStaffClientAsync(string email)
    {
        const string password = StaffTestPassword;
        await WithServiceAsync<UserManager<AppUser>>(async users =>
        {
            var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException($"Нет сотрудника {email}");
            if (await users.HasPasswordAsync(user))
                await users.RemovePasswordAsync(user);
            if (!(await users.AddPasswordAsync(user, password)).Succeeded)
                throw new InvalidOperationException("Пароль сотрудника не задан");
        });
        return await CreateClientForAsync(email, password);
    }

    // Создатель платформы входит по паролю, как и другие сотрудники (2FA у него в тестах не включена).
    public Task<HttpClient> CreateSuperAdminClientAsync() => CreateClientForAsync(SuperAdminEmail, SuperAdminPassword);

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
            // Имя базы для DROP DATABASE параметром не передать; оно сгенерировано выше (sitora_test_ + Guid), ввода пользователя здесь нет.
#pragma warning disable CA2100
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string GeneratePassword() =>
        "Aa1-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;
using Sitora.Tests.Security;

namespace Sitora.Tests.Auth;

// R-02 / C-14: имена владельца и создателя платформы — из необязательных секретов OWNER_NAME и SUPERADMIN_NAME, без них —
// часть почты до «@» (раньше сидер писал «Владелец» и «Создатель платформы», и так их видели на всех языках).
public class NamedStagingFactory : StagingFactory
{
    public const string OwnerName = "Некруз Рашидов";
    public const string PlatformName = "Platform Admin";
    public const string PlatformEmail = "platform-named@binosoz.tj";
    public string PlatformPassword { get; } = "Admin-" + Guid.NewGuid().ToString("N")[..10] + "A1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(DatabaseInitializer.OwnerNameVariable, OwnerName);
        builder.UseSetting(DatabaseInitializer.SuperAdminNameVariable, PlatformName);
        builder.UseSetting("ADMIN_EMAIL", PlatformEmail);
        builder.UseSetting("ADMIN_PASSWORD", PlatformPassword);
    }
}

public class SeedNamesTests(NamedStagingFactory factory) : IClassFixture<NamedStagingFactory>
{
    [Fact]
    public async Task Secrets_SetNamesOnCreate_AndUpdateExistingSeederNames_WithAudit()
    {
        factory.CreateClient();
        Assert.Equal(NamedStagingFactory.OwnerName, await NameAsync(StagingFactory.BootstrapEmail));
        Assert.Equal(NamedStagingFactory.PlatformName, await NameAsync(NamedStagingFactory.PlatformEmail));

        // Уже созданные со старыми именами сидера — следующий запуск ставит имена из секретов и пишет журнал.
        await factory.WithDbAsync(db => db.Users.Where(u => u.Email == StagingFactory.BootstrapEmail || u.Email == NamedStagingFactory.PlatformEmail)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FullName, "Старое имя")));
        await factory.Services.InitializeDatabaseAsync();
        Assert.Equal(NamedStagingFactory.OwnerName, await NameAsync(StagingFactory.BootstrapEmail));
        Assert.Equal(NamedStagingFactory.PlatformName, await NameAsync(NamedStagingFactory.PlatformEmail));
        Assert.Equal(2, await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.Action == DatabaseInitializer.NameSetAction)));

        // Повтор без изменений — журнал не растёт.
        await factory.Services.InitializeDatabaseAsync();
        Assert.Equal(2, await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.Action == DatabaseInitializer.NameSetAction)));
    }

    private Task<string> NameAsync(string email) =>
        factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.FullName).SingleAsync());
}

public class SeedNamesWithoutSecretTests(StagingFactory factory) : IClassFixture<StagingFactory>
{
    [Fact]
    public async Task WithoutSecret_NameIsEmailLocalPart_OldSeederNameIsReplaced_OwnNameIsKept()
    {
        factory.CreateClient();
        Assert.Equal("owner", await NameAsync());

        await SetNameAsync("Владелец");
        await factory.Services.InitializeDatabaseAsync();
        Assert.Equal("owner", await NameAsync());

        await SetNameAsync("Фаридун Рашидов");
        await factory.Services.InitializeDatabaseAsync();
        Assert.Equal("Фаридун Рашидов", await NameAsync());
    }

    [Theory]
    [InlineData(null, "owner@nekruz.tj", "owner")]
    [InlineData("  ", "a.b@x.tj", "a.b")]
    [InlineData(" Некруз ", "owner@nekruz.tj", "Некруз")]
    public void SeedName_FromSecretOrEmail(string? configured, string email, string expected) =>
        Assert.Equal(expected, DatabaseInitializer.SeedName(configured, email));

    private Task SetNameAsync(string name) => factory.WithDbAsync(db => db.Users.Where(u => u.Email == StagingFactory.BootstrapEmail)
        .ExecuteUpdateAsync(s => s.SetProperty(u => u.FullName, name)));

    private Task<string> NameAsync() =>
        factory.WithDbAsync(db => db.Users.Where(u => u.Email == StagingFactory.BootstrapEmail).Select(u => u.FullName).SingleAsync());
}

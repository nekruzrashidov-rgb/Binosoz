using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Tests.Infrastructure;
using Sitora.Tests.Security;

namespace Sitora.Tests.Auth;

// Development: для почт из Auth:DevFixedCodeEmails код из письма (подтверждение регистрации, «Забыли код?») можно
// заменить на 123456. Список задаётся в user-secrets разработчика (личная почта в репозиторий не попадает);
// тест задаёт свою тестовую почту.
public class DevFixedCodeEmailFactory : SitoraApiFactory
{
    public const string DevEmail = "dev-fixed-code@mail.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:DevFixedCodeEmails:0", DevEmail);
    }
}

public class DevFixedCodeTests(DevFixedCodeEmailFactory factory) : IClassFixture<DevFixedCodeEmailFactory>
{
    private const string DevEmail = DevFixedCodeEmailFactory.DevEmail;

    [Fact]
    public async Task ConfiguredEmail_SetsPinWith123456_OtherEmailsDoNot()
    {
        var manager = await factory.CreateManagerClientAsync();
        (await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Разработчик", SitoraApiFactory.RandomPhone(), DevEmail, null, null))).EnsureSuccessStatusCode();
        var client = factory.CreateClient();

        var fixedCode = await client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset",
            new CustomerPinResetRequest(DevEmail, "123456", SitoraApiFactory.CustomerTestPin));
        Assert.Equal(HttpStatusCode.OK, fixedCode.StatusCode);
        Assert.Equal("Customer", (await fixedCode.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!.User.Role);

        // Для остальных почт 123456 — обычный неверный код.
        var other = $"other-{Guid.NewGuid():N}@mail.test";
        (await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Другой", SitoraApiFactory.RandomPhone(), other, null, null))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset-request", new CustomerEmailRequest(other))).EnsureSuccessStatusCode();
        var code = factory.Email.LastCode(other);
        var wrong = code == "123456" ? "654321" : "123456";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset",
            new CustomerPinResetRequest(other, wrong, SitoraApiFactory.CustomerTestPin))).StatusCode);
    }
}

// Вне Development список не действует, даже если его задать.
public class StagingFixedCodeFactory : StagingFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:DevFixedCodeEmails:0", BootstrapEmail);
        builder.UseSetting("Auth:DevFixedCodeForAnyEmail", "true");
    }
}

public class StagingFixedCodeTests(StagingFixedCodeFactory factory) : IClassFixture<StagingFixedCodeFactory>
{
    [Fact]
    public async Task OutsideDevelopment_FixedCodeIsRejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/customer/pin/reset",
            new CustomerPinResetRequest(StagingFactory.BootstrapEmail, "123456", SitoraApiFactory.CustomerTestPin));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

// Development с Auth:DevFixedCodeForAnyEmail: 123456 подтверждает регистрацию с любой почтой; сотрудникам код
// входа так не задать.
public class AnyEmailFixedCodeFactory : SitoraApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:DevFixedCodeForAnyEmail", "true");
    }
}

public class AnyEmailFixedCodeTests(AnyEmailFixedCodeFactory factory) : IClassFixture<AnyEmailFixedCodeFactory>
{
    [Fact]
    public async Task AnyEmail_ConfirmsWith123456_StaffAndSuperAdminCannotGetPin()
    {
        var client = factory.CreateClient();
        var email = $"new-{Guid.NewGuid():N}@mail.test";
        (await client.PostAsJsonAsync("/api/v1/auth/customer/register", new CustomerRegisterRequest("Новый Покупатель", email,
            SitoraApiFactory.RandomPhone(), SitoraApiFactory.CustomerTestPin, Organization.DefaultSlug))).EnsureSuccessStatusCode();

        var confirmed = await client.PostAsJsonAsync("/api/v1/auth/customer/confirm", new CustomerConfirmRequest(email, "123456"));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("Customer", (await confirmed.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!.User.Role);

        // Код разработки не задаёт код входа сотрудникам и создателю платформы и не делает из них покупателей.
        foreach (var staff in new[] { SitoraApiFactory.ManagerEmail, SitoraApiFactory.SuperAdminEmail })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset",
                new CustomerPinResetRequest(staff, "123456", SitoraApiFactory.CustomerTestPin))).StatusCode);
        Assert.False(await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email == SitoraApiFactory.ManagerEmail)));
        // Сотрудник по-прежнему входит паролем.
        await factory.CreateManagerClientAsync();

        // Незнакомая почта без карточки клиента кодом разработки покупателем не становится.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset",
            new CustomerPinResetRequest($"x-{Guid.NewGuid():N}@mail.test", "123456", SitoraApiFactory.CustomerTestPin))).StatusCode);
    }
}

// Production с Auth:DevFixedCodeForAnyEmail и Auth:DevFixedCodeEmails: оба флага игнорируются полностью.
public class ProductionFixedCodeFactory : ProductionFactory
{
    public const string BuyerEmail = "prod-fixed-code@mail.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:DevFixedCodeEmails:0", BuyerEmail);
        builder.UseSetting("Auth:DevFixedCodeForAnyEmail", "true");
    }
}

public class ProductionFixedCodeTests(ProductionFixedCodeFactory factory) : IClassFixture<ProductionFixedCodeFactory>
{
    [Theory]
    [InlineData(ProductionFixedCodeFactory.BuyerEmail)]
    [InlineData("any-other-buyer@mail.test")]
    public async Task Production_FixedCodeIsIgnored(string email)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://api.sitora.test") });

        var reset = await client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset",
            new CustomerPinResetRequest(email, "123456", SitoraApiFactory.CustomerTestPin));
        var confirm = await client.PostAsJsonAsync("/api/v1/auth/customer/confirm", new CustomerConfirmRequest(email, "123456"));

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (reset.StatusCode, confirm.StatusCode));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Sitora.Infrastructure.Data.AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Sales;
using Sitora.Tests.Infrastructure;
using Sitora.Tests.Security;

namespace Sitora.Tests.Auth;

// Development: для почт из Auth:DevFixedCodeEmails подходит код 123456. Список задаётся в user-secrets разработчика
// (личная почта в репозиторий не попадает); тест задаёт свою тестовую почту.
public class DevFixedCodeEmailFactory : SitoraApiFactory
{
    public const string DevEmail = "dev-fixed-code@mail.test";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:DevFixedCodeEmails:0", DevEmail);
    }
}

public class DevFixedCodeTests(DevFixedCodeEmailFactory factory) : IClassFixture<DevFixedCodeEmailFactory>
{
    private const string DevEmail = DevFixedCodeEmailFactory.DevEmail;

    [Fact]
    public async Task ConfiguredEmail_SignsInWith123456_OtherEmailsDoNot()
    {
        var manager = await factory.CreateManagerClientAsync();
        (await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Разработчик", SitoraApiFactory.RandomPhone(), DevEmail, null, null))).EnsureSuccessStatusCode();
        var client = factory.CreateClient();

        var fixedCode = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new EmailVerifyRequest(DevEmail, "123456"));
        Assert.Equal(HttpStatusCode.OK, fixedCode.StatusCode);
        Assert.Equal("Customer", (await fixedCode.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!.User.Role);

        // Для остальных почт 123456 — обычный неверный код.
        var other = $"other-{Guid.NewGuid():N}@mail.test";
        (await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Другой", SitoraApiFactory.RandomPhone(), other, null, null))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/auth/email/send", new EmailSendRequest(other))).EnsureSuccessStatusCode();
        var code = factory.Email.LastCode(other);
        var wrong = code == "123456" ? "654321" : "123456";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/email/verify", new EmailVerifyRequest(other, wrong))).StatusCode);
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
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/email/verify",
            new EmailVerifyRequest(StagingFactory.BootstrapEmail, "123456"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

// Development с Auth:DevFixedCodeForAnyEmail: 123456 подходит для любой почты, неизвестная почта становится покупателем.
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
    public async Task UnknownEmail_BecomesCustomer_StaffAndSuperAdminStillNeedPassword()
    {
        var client = factory.CreateClient();
        var email = $"New-{Guid.NewGuid():N}@Mail.Test";

        var created = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new EmailVerifyRequest(email, "123456"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("Customer", (await created.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!.User.Role);
        var card = await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Email == email.ToLowerInvariant()));
        Assert.Equal(Sitora.Domain.Entities.Organization.DefaultId, card.OrganizationId);

        // Второй вход той же почтой не создаёт ещё одну карточку.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/email/verify", new EmailVerifyRequest(email, "123456"))).StatusCode);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters().CountAsync(c => c.Email == email.ToLowerInvariant())));

        // Код разработки не открывает вход сотрудникам и создателю платформы и не делает из них покупателей.
        foreach (var staff in new[] { SitoraApiFactory.ManagerEmail, SitoraApiFactory.SuperAdminEmail })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/email/verify", new EmailVerifyRequest(staff, "123456"))).StatusCode);
        Assert.False(await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email == SitoraApiFactory.ManagerEmail)));

        // Неверный код по-прежнему не подходит.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/email/verify",
            new EmailVerifyRequest($"x-{Guid.NewGuid():N}@mail.test", "654321"))).StatusCode);
    }
}

// Production с Auth:DevFixedCodeForAnyEmail и Auth:DevFixedCodeEmails: оба флага игнорируются полностью.
// Почта покупателя, а не сотрудника: в Development такая почта с 123456 вошла бы и стала покупателем.
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

        var response = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new EmailVerifyRequest(email, "123456"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // Покупатель по коду разработки не создан.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Sitora.Infrastructure.Data.AppDbContext>();
        Assert.False(await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email == email));
    }
}

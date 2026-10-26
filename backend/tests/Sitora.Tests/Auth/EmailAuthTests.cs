using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

public class EmailAuthTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Send_AnswersTheSame_ForKnownAndUnknownEmail_AndMailsOnlyKnown()
    {
        var unknown = $"nobody-{Guid.NewGuid():N}@nowhere.test";

        var known = await _client.PostAsJsonAsync("/api/v1/auth/email/send", new EmailSendRequest(SitoraApiFactory.ManagerEmail));
        var missing = await _client.PostAsJsonAsync("/api/v1/auth/email/send", new EmailSendRequest(unknown));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (known.StatusCode, missing.StatusCode));
        Assert.Equal(await known.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
        Assert.Equal(0, factory.Email.SentCount(unknown));
        Assert.NotNull(factory.Email.LastCode(SitoraApiFactory.ManagerEmail));

        // Код хранится только хешем.
        var code = factory.Email.LastCode(SitoraApiFactory.ManagerEmail)!;
        await factory.WithDbAsync(async db => Assert.False(await db.EmailCodes.AnyAsync(c => c.CodeHash.Contains(code))));

        // По коду для незнакомой почты войти нельзя.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify(unknown, "123456")).StatusCode);
    }

    [Fact]
    public async Task Verify_StaffGetsTokensWithRoleAndOrganization()
    {
        await Send(SitoraApiFactory.ManagerEmail);

        // Почта нормализуется: регистр и пробелы не важны.
        var response = await Verify("  MANAGER@Sitora.Local ", factory.Email.LastCode(SitoraApiFactory.ManagerEmail)!);

        var auth = await TestScenarios.ReadAsync<AuthResponse>(response);
        Assert.Equal("Manager", auth.User.Role);
        Assert.Equal(Sitora.Domain.Entities.Organization.DefaultId, auth.User.OrganizationId);
        Assert.Empty(auth.User.BuildingIds!);
        Assert.Null(auth.User.Impersonation);
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
    }

    [Fact]
    public async Task Verify_WrongCode_ThenFiveAttemptsBurnTheCode()
    {
        var email = await NewCustomerEmailAsync();
        await Send(email);
        var code = factory.Email.LastCode(email)!;
        var wrong = code == "000000" ? "111111" : "000000";

        var first = await Verify(email, wrong);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal("Неверный или устаревший код", (await first.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
        for (var i = 1; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Verify(email, wrong)).StatusCode);

        // После пяти ошибок не подходит и верный код.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify(email, code)).StatusCode);
    }

    [Fact]
    public async Task Verify_CodeWorksOnce_OnlyLatestCodeCounts_AndExpires()
    {
        var email = await NewCustomerEmailAsync();

        await Send(email);
        var code = factory.Email.LastCode(email)!;
        Assert.Equal(HttpStatusCode.OK, (await Verify(email, code)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify(email, code)).StatusCode);

        // Новый код отменяет прежний.
        await Send(email);
        var older = factory.Email.LastCode(email)!;
        await Send(email);
        var latest = factory.Email.LastCode(email)!;
        if (older != latest)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Verify(email, older)).StatusCode);

        // Истёкший код не принимается.
        await factory.WithDbAsync(db => db.EmailCodes.Where(c => c.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, DateTime.UtcNow.AddSeconds(-1))));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify(email, latest)).StatusCode);
    }

    [Fact]
    public async Task Verify_CustomerIsLinkedByDealEmail_AndSeesOnlyOwnDeals()
    {
        var manager = await factory.CreateManagerClientAsync();
        var email = $"Buyer-{Guid.NewGuid():N}@Mail.Test";
        var customer = await TestScenarios.ReadAsync<CustomerResponse>(await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Покупатель по почте", SitoraApiFactory.RandomPhone(), $"  {email} ", null, null)));
        Assert.Equal(email.ToLowerInvariant(), customer.Email);
        var deal = await TestScenarios.ReadAsync<DealResponse>(await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(await factory.FindFreeApartmentAsync(), customer.Id, DealStage.Contract, Currency.TJS, null, null),
            TestJson.Options));
        var foreign = await TestScenarios.CreateDealAsync(factory);

        var client = await factory.CreateEmailClientAsync(email.ToLowerInvariant());

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options);
        Assert.Equal("Customer", me!.Role);
        var deals = await client.GetFromJsonAsync<List<DealResponse>>("/api/v1/me/deals", TestJson.Options);
        Assert.Equal([deal.Id], deals!.Select(d => d.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/deals/{foreign.DealId}/schedule")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/deals")).StatusCode);
    }

    [Fact]
    public async Task PlatformOwner_NeedsSecondFactor_AndCannotUsePasswordLogin()
    {
        await Send(SitoraApiFactory.PlatformOwnerEmail);
        var verified = await Verify(SitoraApiFactory.PlatformOwnerEmail, factory.Email.LastCode(SitoraApiFactory.PlatformOwnerEmail)!);

        Assert.Equal(HttpStatusCode.Accepted, verified.StatusCode);
        var body = await verified.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body);
        var challenge = (await verified.Content.ReadFromJsonAsync<SecondFactorChallenge>(TestJson.Options))!;
        Assert.Contains("password", challenge.Methods, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/auth/second-factor",
            new SecondFactorRequest(challenge.ChallengeToken, "Wrong-password-1", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/auth/second-factor",
            new SecondFactorRequest("not-a-challenge", factory.PlatformOwnerPassword, null))).StatusCode);

        var auth = await TestScenarios.ReadAsync<AuthResponse>(await _client.PostAsJsonAsync("/api/v1/auth/second-factor",
            new SecondFactorRequest(challenge.ChallengeToken, factory.PlatformOwnerPassword, null)));
        Assert.Equal("PlatformOwner", auth.User.Role);
        Assert.Null(auth.User.OrganizationId);

        // Вызов использован — повторно не сработает.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/auth/second-factor",
            new SecondFactorRequest(challenge.ChallengeToken, factory.PlatformOwnerPassword, null))).StatusCode);

        // Вход по паролю без кода из почты закрыт, ответ как на неверный пароль.
        var password = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(SitoraApiFactory.PlatformOwnerEmail, factory.PlatformOwnerPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, password.StatusCode);
        Assert.Equal("Неверная почта или пароль", (await password.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
    }

    private Task Send(string email) =>
        _client.PostAsJsonAsync("/api/v1/auth/email/send", new EmailSendRequest(email)).ContinueWith(t => t.Result.EnsureSuccessStatusCode());

    private Task<HttpResponseMessage> Verify(string email, string code) =>
        _client.PostAsJsonAsync("/api/v1/auth/email/verify", new EmailVerifyRequest(email, code));

    private async Task<string> NewCustomerEmailAsync()
    {
        var email = $"c-{Guid.NewGuid():N}@mail.test";
        var manager = await factory.CreateManagerClientAsync();
        (await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Покупатель", SitoraApiFactory.RandomPhone(), email, null, null))).EnsureSuccessStatusCode();
        return email;
    }
}

// Лимиты по умолчанию: не чаще раза в минуту и не больше пяти в час на почту; SMS-вход выключен.
public class EmailLimitsFactory : SitoraApiFactory
{
    protected override bool SmsLoginEnabled => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:EmailCodesPerMinute", "1");
        builder.UseSetting("Auth:EmailCodesPerHour", "5");
    }
}

public class EmailLimitTests(EmailLimitsFactory factory) : IClassFixture<EmailLimitsFactory>
{
    [Fact]
    public async Task Send_OncePerMinute_AndFivePerHour_EvenForUnknownEmail()
    {
        var client = factory.CreateClient();
        foreach (var email in new[] { SitoraApiFactory.ContentEmail, $"ghost-{Guid.NewGuid():N}@nowhere.test" })
        {
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(HttpStatusCode.OK, (await Send(client, email)).StatusCode);
                var again = await Send(client, email);
                Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
                Assert.Contains("через минуту", await again.Content.ReadAsStringAsync());
                await ShiftBackAsync(email, TimeSpan.FromMinutes(2));
            }

            var hourly = await Send(client, email);
            Assert.Equal(HttpStatusCode.TooManyRequests, hourly.StatusCode);
            Assert.Contains("Попробуйте позже", await hourly.Content.ReadAsStringAsync());

            // Через час лимит снова свободен.
            await ShiftBackAsync(email, TimeSpan.FromHours(1));
            Assert.Equal(HttpStatusCode.OK, (await Send(client, email)).StatusCode);
        }
    }

    [Fact]
    public async Task SmsLogin_IsDisabledByFlag()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/auth/sms/send", new { phone = "+992771234567" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/auth/sms/verify", new { phone = "+992771234567", code = "123456" })).StatusCode);
        Assert.Equal(0, factory.Sms.SentCount("+992771234567"));
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/v1/auth/email/send", new EmailSendRequest(email));

    private Task ShiftBackAsync(string email, TimeSpan by) =>
        factory.WithDbAsync(db => db.EmailCodes.Where(c => c.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, c => c.CreatedAt - by)));
}

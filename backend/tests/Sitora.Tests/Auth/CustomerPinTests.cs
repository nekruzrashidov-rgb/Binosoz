using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Вход покупателя по своему 6-значному коду (PIN): хеш Identity, блокировка 5/15 мин, сброс через код из почты.
public class CustomerPinTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private const string Pin = SitoraApiFactory.CustomerTestPin;
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("000000")]
    [InlineData("777777")]
    [InlineData("123456")]
    [InlineData("654321")]
    [InlineData("012345")]
    [InlineData("456789")]
    [InlineData("987654")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public async Task WeakOrMalformedPin_IsRejected_OnRegisterAndReset(string pin)
    {
        Assert.False(PinRules.IsValid(pin));
        var email = $"weak-{Guid.NewGuid():N}@mail.test";

        var register = await _client.PostAsJsonAsync("/api/v1/auth/customer/register",
            new CustomerRegisterRequest("Слабый Код", email, SitoraApiFactory.RandomPhone(), pin, Organization.DefaultSlug));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, register.StatusCode);
        Assert.Matches("pin_weak|pin_format", await register.Content.ReadAsStringAsync());
        // Ничего не создано и письмо не ушло.
        Assert.False(await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email == email)));
        Assert.Equal(0, factory.Email.SentCount(email));

        var reset = await _client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset", new CustomerPinResetRequest(email, "111111", pin));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reset.StatusCode);
    }

    [Theory]
    [InlineData("482915")]
    [InlineData("112358")]
    [InlineData("135790")]
    public void GoodPin_Passes(string pin) => Assert.True(PinRules.IsValid(pin));

    [Fact]
    public async Task PinRules_AreOnlyForCustomers_StaffKeepsPasswordRules()
    {
        await factory.WithServiceAsync<UserManager<AppUser>>(async users =>
        {
            var manager = (await users.FindByEmailAsync(SitoraApiFactory.ManagerEmail))!;
            // Сотруднику 6 цифр паролем не годятся: нужны буквы, как раньше.
            var staff = await users.PasswordValidators.Single().ValidateAsync(users, manager, "482915");
            Assert.False(staff.Succeeded);
            Assert.True((await users.PasswordValidators.Single().ValidateAsync(users, manager, "Staff-Test-1")).Succeeded);
        });

        var email = await NewCustomerAsync();
        await factory.WithServiceAsync<UserManager<AppUser>>(async users =>
        {
            var customer = (await users.FindByEmailAsync(email))!;
            Assert.True((await users.PasswordValidators.Single().ValidateAsync(users, customer, "482915")).Succeeded);
            // А покупателю не годится пароль сотрудника.
            Assert.False((await users.PasswordValidators.Single().ValidateAsync(users, customer, "Staff-Test-1")).Succeeded);
        });
    }

    [Fact]
    public async Task Login_WrongPin_ShowsAttemptsLeft_FiveFailures_Lock15Minutes_AndAudit()
    {
        var email = await NewCustomerAsync();
        var userId = await UserIdAsync(email);

        for (var left = 4; left >= 1; left--)
        {
            var wrong = await Login(email, "135790");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            var body = await wrong.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid_pin", body.GetProperty("code").GetString());
            Assert.Equal(left, body.GetProperty("attemptsLeft").GetInt32());
            Assert.EndsWith($"Осталось попыток: {left}", body.GetProperty("detail").GetString());
        }

        var fifth = await Login(email, "135790");
        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
        var locked = await fifth.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pin_locked", locked.GetProperty("code").GetString());
        var until = locked.GetProperty("lockedUntil").GetDateTime().ToUniversalTime();
        Assert.InRange(until, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        Assert.InRange(locked.GetProperty("retryAfterSeconds").GetInt32(), 14 * 60, 15 * 60);

        // Пока блокировка, не подходит и верный код.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(email, Pin)).StatusCode);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == userId && l.Action == "PinLockedOut")));

        // Через 15 минут — снова можно.
        await factory.WithDbAsync(db => db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddSeconds(-1))));
        var auth = await TestScenarios.ReadAsync<AuthResponse>(await Login(email, Pin));
        Assert.Equal(("Customer", userId), (auth.User.Role, auth.User.Id));
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == userId && l.Action == "Login")));
    }

    [Fact]
    public async Task Login_UnknownEmail_AnswersLikeWrongPin_AndLocksTheSameWay()
    {
        var unknown = $"nobody-{Guid.NewGuid():N}@nowhere.test";
        var known = await NewCustomerAsync();

        var a = await (await Login(unknown, "135790")).Content.ReadFromJsonAsync<JsonElement>();
        var b = await (await Login(known, "135790")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(a.GetProperty("code").GetString(), b.GetProperty("code").GetString());
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
        Assert.Equal(a.GetProperty("attemptsLeft").GetInt32(), b.GetProperty("attemptsLeft").GetInt32());
        // Почта сотрудника — тоже как незнакомая.
        var staff = await (await Login(SitoraApiFactory.AccountantEmail, "135790")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_pin", staff.GetProperty("code").GetString());

        for (var i = 0; i < 3; i++)
            await Login(unknown, "135790");
        var locked = await Login(unknown, "135790");
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Equal("pin_locked", (await locked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_CustomerCreatedByManager_GetsPinNotSet_ThenForgotPinSetsIt()
    {
        var manager = await factory.CreateManagerClientAsync();
        var email = $"by-manager-{Guid.NewGuid():N}@mail.test";
        var customer = await TestScenarios.ReadAsync<CustomerResponse>(await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Покупатель менеджера", SitoraApiFactory.RandomPhone(), email, null, null)));
        var deal = await TestScenarios.ReadAsync<DealResponse>(await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(await factory.FindFreeApartmentAsync(), customer.Id, DealStage.Contract, Currency.TJS, null, null),
            TestJson.Options));

        var first = await Login(email, Pin);
        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        Assert.Equal("pin_not_set", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        // Регистрация с этой почтой тоже ведёт в «Забыли код?», если учётная запись уже есть: проверяем после сброса ниже.

        var client = await factory.CreateEmailClientAsync(email);
        var deals = await client.GetFromJsonAsync<List<DealResponse>>("/api/v1/me/deals", TestJson.Options);
        Assert.Equal([deal.Id], deals!.Select(d => d.Id));
        Assert.Equal(HttpStatusCode.OK, (await Login(email, Pin)).StatusCode);
    }

    [Fact]
    public async Task Reset_SameAnswerForAnyEmail_MailsOnlyCustomer_CodeIsHashed()
    {
        var customer = await NewCustomerAsync();
        var unknown = $"nobody-{Guid.NewGuid():N}@nowhere.test";
        var staffBefore = factory.Email.SentCount(SitoraApiFactory.AccountantEmail);
        var customerBefore = factory.Email.SentCount(customer);

        var known = await ResetRequest(customer);
        var staff = await ResetRequest(SitoraApiFactory.AccountantEmail);
        var missing = await ResetRequest(unknown);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), (known.StatusCode, staff.StatusCode, missing.StatusCode));
        var body = await known.Content.ReadAsStringAsync();
        Assert.Equal(body, await missing.Content.ReadAsStringAsync());
        Assert.Equal(body, await staff.Content.ReadAsStringAsync());
        Assert.Equal(0, factory.Email.SentCount(unknown));
        Assert.Equal(staffBefore, factory.Email.SentCount(SitoraApiFactory.AccountantEmail));
        Assert.Equal(customerBefore + 1, factory.Email.SentCount(customer));
        var message = factory.Email.LastMessage(customer)!;
        Assert.Contains("код для сброса кода входа", message.Subject);
        Assert.Contains("Никому не сообщайте код", message.Text);

        var code = factory.Email.LastCode(customer)!;
        await factory.WithDbAsync(async db => Assert.False(await db.EmailCodes.AnyAsync(c => c.CodeHash.Contains(code))));
    }

    [Fact]
    public async Task Reset_MailServiceFailure_AnswersTheSame()
    {
        var email = await NewCustomerAsync();
        factory.Email.Failing[email] = true;
        try
        {
            var failed = await ResetRequest(email);
            var unknown = await ResetRequest($"nobody-{Guid.NewGuid():N}@nowhere.test");
            Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (failed.StatusCode, unknown.StatusCode));
            Assert.Equal(await unknown.Content.ReadAsStringAsync(), await failed.Content.ReadAsStringAsync());
        }
        finally
        {
            factory.Email.Failing.TryRemove(email, out _);
        }
    }

    [Fact]
    public async Task Reset_RightCode_ChangesPin_UnlocksAndRevokesAllRefreshTokens()
    {
        var email = await NewCustomerAsync();
        var userId = await UserIdAsync(email);
        var oldSession = await TestScenarios.ReadAsync<AuthResponse>(await Login(email, Pin));
        // Блокируем вход пятью ошибками.
        for (var i = 0; i < 5; i++)
            await Login(email, "135790");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(email, Pin)).StatusCode);

        (await ResetRequest(email)).EnsureSuccessStatusCode();
        const string newPin = "250817";
        var auth = await TestScenarios.ReadAsync<AuthResponse>(await _client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset",
            new CustomerPinResetRequest(email, factory.Email.LastCode(email)!, newPin)));
        Assert.Equal(userId, auth.User.Id);

        // Прежние сессии закрыты, новая работает.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(oldSession.RefreshToken))).StatusCode);
        Assert.False(await factory.WithDbAsync(db => db.RefreshTokens.AnyAsync(t => t.UserId == userId && t.RevokedAt == null && t.TokenHash != JwtHash(auth.RefreshToken))));
        // Блокировка снята, старый код не подходит, новый — подходит.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, Pin)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(email, newPin)).StatusCode);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == userId && l.Action == "PinReset")));
    }

    [Fact]
    public async Task Reset_WrongCode_BurnsAfterFive_ExpiredAndRegisterCodeDoNotWork()
    {
        var email = await NewCustomerAsync();
        (await ResetRequest(email)).EnsureSuccessStatusCode();
        var code = factory.Email.LastCode(email)!;
        var wrong = code == "000000" ? "111111" : "000000";

        var first = await Reset(email, wrong);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal("invalid_code", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        for (var i = 1; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Reset(email, wrong)).StatusCode);
        // После пяти ошибок не подходит и верный код.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Reset(email, code)).StatusCode);

        // Просроченный код не принимается.
        (await ResetRequest(email)).EnsureSuccessStatusCode();
        var fresh = factory.Email.LastCode(email)!;
        await factory.WithDbAsync(db => db.EmailCodes.Where(c => c.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, DateTime.UtcNow.AddSeconds(-1))));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Reset(email, fresh)).StatusCode);

        // Код работает один раз.
        (await ResetRequest(email)).EnsureSuccessStatusCode();
        var once = factory.Email.LastCode(email)!;
        Assert.Equal(HttpStatusCode.OK, (await Reset(email, once)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Reset(email, once)).StatusCode);
    }

    [Fact]
    public async Task Confirm_ResetCodeDoesNotConfirmRegistration()
    {
        var email = $"purpose-{Guid.NewGuid():N}@mail.test";
        (await _client.PostAsJsonAsync("/api/v1/auth/customer/register", new CustomerRegisterRequest("Покупатель Цель", email,
            SitoraApiFactory.RandomPhone(), Pin, Organization.DefaultSlug))).EnsureSuccessStatusCode();
        (await ResetRequest(email)).EnsureSuccessStatusCode();

        // Код «Забыли код?» не подходит для подтверждения регистрации — у кодов разное назначение.
        var confirm = await _client.PostAsJsonAsync("/api/v1/auth/customer/confirm", new CustomerConfirmRequest(email, factory.Email.LastCode(email)!));
        Assert.Equal(HttpStatusCode.Unauthorized, confirm.StatusCode);
    }

    [Fact]
    public async Task StaffStillSignsInWithPassword_CustomerWithPasswordEndpointGets400_MethodsHavePin()
    {
        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal("Manager", (await manager.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options))!.Role);

        var email = await NewCustomerAsync();
        var asStaff = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Pin));
        Assert.Equal(HttpStatusCode.BadRequest, asStaff.StatusCode);
        Assert.Equal("use_code_login", (await asStaff.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var methods = await _client.GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.True(methods.GetProperty("pin").GetBoolean());
        Assert.True(methods.GetProperty("email").GetBoolean());
    }

    [Fact]
    public async Task OldEmailCodeEndpoints_AreRemoved()
    {
        // Маршрутов больше нет: POST на них — 404 или 405 (адрес принадлежит статике сайта).
        foreach (var url in new[] { "/api/v1/auth/email/send", "/api/v1/auth/email/verify", "/api/v1/auth/register" })
        {
            var response = await _client.PostAsJsonAsync(url, new { email = "a@b.test", code = "123456" });
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        }
    }

    [Fact]
    public async Task Tajik_InvalidPinText()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "tg");
        var response = await client.PostAsJsonAsync("/api/v1/auth/customer/login", new CustomerLoginRequest($"tg-{Guid.NewGuid():N}@mail.test", "135790"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Почта ё рамзи воридшавӣ нодуруст аст. Кӯшишҳои боқимонда: 4", body.GetProperty("detail").GetString());
    }

    // Покупатель с подтверждённой почтой и кодом входа Pin.
    private async Task<string> NewCustomerAsync()
    {
        var email = $"c-{Guid.NewGuid():N}@mail.test";
        var manager = await factory.CreateManagerClientAsync();
        (await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Покупатель", SitoraApiFactory.RandomPhone(), email, null, null))).EnsureSuccessStatusCode();
        await factory.CreateEmailClientAsync(email);
        return email;
    }

    private Task<Guid> UserIdAsync(string email) =>
        factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

    private static string JwtHash(string token) => Sitora.Infrastructure.Security.JwtTokenService.HashToken(token);

    private Task<HttpResponseMessage> Login(string email, string pin) =>
        _client.PostAsJsonAsync("/api/v1/auth/customer/login", new CustomerLoginRequest(email, pin));

    private Task<HttpResponseMessage> ResetRequest(string email) =>
        _client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset-request", new CustomerEmailRequest(email));

    private Task<HttpResponseMessage> Reset(string email, string code) =>
        _client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset", new CustomerPinResetRequest(email, code, "250817"));
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
    public async Task CodeRequests_OncePerMinute_AndFivePerHour_EvenForUnknownEmail()
    {
        var client = factory.CreateClient();
        foreach (var email in new[] { SitoraApiFactory.ContentEmail, $"ghost-{Guid.NewGuid():N}@nowhere.test" })
        {
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(HttpStatusCode.OK, (await Send(client, email)).StatusCode);
                // Повтор кода подтверждения считается в том же лимите.
                var again = await client.PostAsJsonAsync("/api/v1/auth/customer/confirm/resend", new CustomerEmailRequest(email));
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
        client.PostAsJsonAsync("/api/v1/auth/customer/pin/reset-request", new CustomerEmailRequest(email));

    private Task ShiftBackAsync(string email, TimeSpan by) =>
        factory.WithDbAsync(db => db.EmailCodes.Where(c => c.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, c => c.CreatedAt - by)));
}

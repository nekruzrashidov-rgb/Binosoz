using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

public class SmsAuthTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task Customer_LogsInWithSmsCode_AndGetsCustomerRole()
    {
        var phone = await CreateCustomerAsync();

        var sent = await _guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var code = factory.Sms.LastCode(phone);
        Assert.Matches("^[0-9]{6}$", code);

        var verified = await _guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, code!));
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var auth = await verified.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Contains(new JsonWebToken(auth!.AccessToken).Claims, c => c.Type == "role" && c.Value == "Customer");
        Assert.Equal(phone, auth.User.Phone);

        var reused = await _guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, code!));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        // Повторный вход использует ту же учётную запись.
        var again = await factory.CreateCustomerClientAsync(phone);
        var me = await again.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me");
        Assert.Equal(auth.User.Id, me!.Id);
        var linked = await factory.WithDbAsync(db => db.Customers.Where(c => c.Phone == phone).Select(c => c.UserId).SingleAsync());
        Assert.Equal(auth.User.Id, linked);
    }

    [Fact]
    public async Task UnknownPhone_GetsSameAnswer_ButNoSms()
    {
        var phone = SitoraApiFactory.RandomPhone();

        var sent = await _guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));

        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        Assert.Equal(0, factory.Sms.SentCount(phone));
        var verify = await _guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, "123456"));
        Assert.Equal(HttpStatusCode.Unauthorized, verify.StatusCode);
    }

    [Fact]
    public async Task WrongCode_Returns401_AndCodeIsBlockedAfterFiveAttempts()
    {
        var phone = await CreateCustomerAsync();
        await _guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        var code = factory.Sms.LastCode(phone)!;
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < SmsCode.MaxAttempts; i++)
        {
            var response = await _guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, wrong));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var correctButBlocked = await _guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, code));
        Assert.Equal(HttpStatusCode.Unauthorized, correctButBlocked.StatusCode);
        Assert.Contains("Неверный или устаревший код", await correctButBlocked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SixthCodeWithinHour_Returns429()
    {
        var phone = await CreateCustomerAsync();

        for (var i = 0; i < AuthService.MaxSmsCodesPerHour; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await _guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone))).StatusCode);

        var limited = await _guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
        Assert.Equal(AuthService.MaxSmsCodesPerHour, factory.Sms.SentCount(phone));
    }

    [Fact]
    public async Task InvalidCodeFormat_Returns400()
    {
        var response = await _guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest("+992901234567", "12ab"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("6 цифр", await response.Content.ReadAsStringAsync());
    }

    private async Task<string> CreateCustomerAsync()
    {
        var phone = SitoraApiFactory.RandomPhone();
        var manager = await factory.CreateManagerClientAsync();
        var response = await manager.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest("Покупатель SMS", phone, null, null, null));
        response.EnsureSuccessStatusCode();
        return phone;
    }
}

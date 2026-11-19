using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Вместо Telegram Gateway: коды складываются сюда; номера из NoTelegram — «нет Telegram», Fail — сбой шлюза.
public class FakeTelegramGateway : ITelegramGateway
{
    public ConcurrentQueue<(string Phone, string Code, int Ttl)> Sent { get; } = new();
    public ConcurrentQueue<(string RequestId, string Code)> Checked { get; } = new();
    public ConcurrentDictionary<string, bool> NoTelegram { get; } = new();
    public bool Fail { get; set; }

    public bool Configured => true;

    public string? LastCode(string phone) => Sent.LastOrDefault(s => s.Phone == phone).Code;

    public Task<GatewaySendResult> SendCodeAsync(string phone, string code, int ttlSeconds, CancellationToken ct = default)
    {
        if (NoTelegram.ContainsKey(phone))
            return Task.FromResult(new GatewaySendResult(GatewaySendStatus.NoTelegram, null, "PHONE_NUMBER_NOT_AVAILABLE"));
        if (Fail)
            return Task.FromResult(new GatewaySendResult(GatewaySendStatus.Failed, null, "INTERNAL"));
        Sent.Enqueue((phone, code, ttlSeconds));
        return Task.FromResult(new GatewaySendResult(GatewaySendStatus.Sent, "req-" + Guid.NewGuid().ToString("N")));
    }

    public Task CheckStatusAsync(string requestId, string code, CancellationToken ct = default)
    {
        Checked.Enqueue((requestId, code));
        return Task.CompletedTask;
    }

    public bool IsValidReport(string? timestamp, string? signature, string body) => signature == "valid";
}

// Вход по телефону кодом в Telegram: SMS выключены, Gateway «настроен»; лимиты кодов — как в бою (1 в минуту, 5 в час).
public class TelegramGatewayFactory : SitoraApiFactory
{
    public FakeTelegramGateway Gateway { get; } = new();

    protected override bool SmsLoginEnabled => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Telegram:Gateway:Token", "test-gateway-token");
        builder.UseSetting("Auth:EmailCodesPerMinute", "1");
        builder.UseSetting("Auth:EmailCodesPerHour", "5");
        builder.ConfigureTestServices(services => services.AddSingleton<ITelegramGateway>(Gateway));
    }
}

public class TelegramGatewayTests(TelegramGatewayFactory factory) : IClassFixture<TelegramGatewayFactory>
{
    private async Task<string> CustomerPhoneAsync() => (await TestScenarios.CreateDealAsync(factory)).Phone;

    [Fact]
    public async Task Methods_SayTelegram_AndLoginWithCodeFromTelegram()
    {
        var guest = factory.CreateClient();
        var methods = (await guest.GetFromJsonAsync<AuthMethodsResponse>("/api/v1/auth/methods"))!;
        Assert.True(methods.Telegram);
        Assert.False(methods.Sms);

        var phone = await CustomerPhoneAsync();
        var sent = await guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var body = (await sent.Content.ReadFromJsonAsync<SmsSendResponse>())!;
        Assert.Equal(600, body.ExpiresInSeconds);
        Assert.Contains("Telegram", body.Message);
        var (_, code, ttl) = factory.Gateway.Sent.Last(s => s.Phone == phone);
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(600, ttl);
        Assert.Equal(0, factory.Sms.SentCount(phone));

        var verified = await guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, code));
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Contains(factory.Gateway.Checked, c => c.Code == code);
    }

    [Fact]
    public async Task NoTelegramOnNumber_ClearError()
    {
        var phone = await CustomerPhoneAsync();
        factory.Gateway.NoTelegram[phone] = true;

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("telegram_no_account", text);
        Assert.Contains("На этот номер нет Telegram — войдите по почте", text);
    }

    [Fact]
    public async Task UnknownNumber_SameAnswer_NothingSentOrPaid()
    {
        var phone = SitoraApiFactory.RandomPhone();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(factory.Gateway.Sent, s => s.Phone == phone);
    }

    [Fact]
    public async Task OncePerMinute_PerNumberAndPerIp()
    {
        var phone = await CustomerPhoneAsync();
        var guest = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Accepted, (await guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone))).StatusCode);
        var again = await guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        Assert.Equal((HttpStatusCode)429, again.StatusCode);
        Assert.Contains("code_cooldown", await again.Content.ReadAsStringAsync());

        // С одного IP — два разных номера за минуту: второй ждёт.
        var (first, second) = (await CustomerPhoneAsync(), await CustomerPhoneAsync());
        await factory.WithServiceAsync<IAuthService>(a => a.SendSmsCodeAsync(new SmsSendRequest(first), "10.20.30.40"));
        await Assert.ThrowsAsync<Sitora.Domain.Exceptions.TooManyRequestsException>(() =>
            factory.WithServiceAsync<IAuthService>(a => a.SendSmsCodeAsync(new SmsSendRequest(second), "10.20.30.40")));
    }

    [Fact]
    public async Task WrongCodeFiveTimes_ThenEvenRightCodeFails()
    {
        var phone = await CustomerPhoneAsync();
        var guest = factory.CreateClient();
        await guest.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        var code = factory.Gateway.LastCode(phone)!;
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < SmsCode.MaxAttempts; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, wrong))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/auth/sms/verify", new SmsVerifyRequest(phone, code))).StatusCode);
    }

    [Fact]
    public async Task DailyLimit_Returns503()
    {
        var phone = await CustomerPhoneAsync();
        var marker = "limit-" + Guid.NewGuid().ToString("N");
        await factory.WithDbAsync(async db =>
        {
            for (var i = 0; i < 200; i++)
                db.SmsCodes.Add(new SmsCode { Phone = "+99200" + i, Channel = SmsCode.ChannelTelegram, CodeHash = "-", GatewayRequestId = marker + i, ExpiresAt = DateTime.UtcNow, UsedAt = DateTime.UtcNow, RequestIp = marker });
            await db.SaveChangesAsync();
        });
        try
        {
            var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("telegram_gateway_limit", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            await factory.WithDbAsync(db => db.SmsCodes.Where(c => c.RequestIp == marker).ExecuteDeleteAsync());
        }
    }

    [Fact]
    public async Task DeliveryReport_BadSignature404_GoodSavesStatus()
    {
        var phone = await CustomerPhoneAsync();
        await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(phone));
        var requestId = await factory.WithDbAsync(db => db.SmsCodes.Where(c => c.Phone == phone && c.GatewayRequestId != null).Select(c => c.GatewayRequestId!).SingleAsync());
        var body = JsonSerializer.Serialize(new { request_id = requestId, delivery_status = new { status = "delivered" } });

        async Task<HttpStatusCode> Post(string signature)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telegram/gateway/report") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add("X-Request-Timestamp", "1700000000");
            request.Headers.Add("X-Request-Signature", signature);
            return (await factory.CreateClient().SendAsync(request)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.NotFound, await Post("forged"));
        Assert.Equal(HttpStatusCode.OK, await Post("valid"));
        Assert.Equal("delivered", await factory.WithDbAsync(db => db.SmsCodes.Where(c => c.GatewayRequestId == requestId).Select(c => c.DeliveryStatus).SingleAsync()));
    }
}

// HTTP-клиент Gateway без сети: порядок вызовов, заголовок, ответ «нет Telegram», подпись отчёта.
public class TelegramGatewayClientTests
{
    private sealed class Handler(Func<string, JsonElement, object> answer) : HttpMessageHandler
    {
        public List<(string Method, JsonElement Body, string? Auth)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var method = request.RequestUri!.AbsolutePath.Trim('/');
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            Calls.Add((method, body, request.Headers.Authorization?.ToString()));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(answer(method, body)) };
        }
    }

    private static TelegramGatewayClient Client(Handler handler) => new(new HttpClient(handler),
        Options.Create(new TelegramOptions { Gateway = new TelegramGatewayOptions { Token = "tok", BaseUrl = "https://gateway.test" } }),
        NullLogger<TelegramGatewayClient>.Instance);

    [Fact]
    public async Task Send_ChecksAbility_ThenSendsOurCodeWithSameRequestId()
    {
        var handler = new Handler((method, _) => new { ok = true, result = new { request_id = "r-1" } });

        var result = await Client(handler).SendCodeAsync("+992900000001", "123456", 600);

        Assert.Equal(new GatewaySendResult(GatewaySendStatus.Sent, "r-1"), result);
        Assert.Equal(["checkSendAbility", "sendVerificationMessage"], handler.Calls.Select(c => c.Method));
        Assert.All(handler.Calls, c => Assert.Equal("Bearer tok", c.Auth));
        var send = handler.Calls[1].Body;
        Assert.Equal(("r-1", "123456", 600), (send.GetProperty("request_id").GetString(), send.GetProperty("code").GetString(), send.GetProperty("ttl").GetInt32()));
        Assert.False(send.TryGetProperty("callback_url", out _));
    }

    [Fact]
    public async Task Send_PhoneWithoutTelegram_IsNoTelegram_NotSent()
    {
        var handler = new Handler((_, _) => new { ok = false, error = "PHONE_NUMBER_NOT_AVAILABLE" });

        var result = await Client(handler).SendCodeAsync("+992900000002", "123456", 600);

        Assert.Equal(GatewaySendStatus.NoTelegram, result.Status);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Send_BadToken_IsFailed()
    {
        var result = await Client(new Handler((_, _) => new { ok = false, error = "ACCESS_TOKEN_INVALID" })).SendCodeAsync("+992900000003", "123456", 600);
        Assert.Equal(GatewaySendStatus.Failed, result.Status);
    }

    [Fact]
    public void Report_SignatureIsHmacOfTimestampAndBody_WithSha256OfToken()
    {
        var client = Client(new Handler((_, _) => new { ok = true }));
        const string body = "{\"request_id\":\"r-1\"}";
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("tok"));
        var signature = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("1700000000\n" + body))).ToLowerInvariant();

        Assert.True(client.IsValidReport("1700000000", signature, body));
        Assert.False(client.IsValidReport("1700000001", signature, body));
        Assert.False(client.IsValidReport("1700000000", signature, body + " "));
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;
using Sitora.Tests.Security;

namespace Sitora.Tests.Infrastructure;

// GmailEmailSender без настоящего Google: заглушка HttpMessageHandler отвечает за токен и отправку.
public partial class GmailEmailSenderTests
{
    // Тестовые значения, не настоящие ключи.
    private static EmailOptions Options(bool configured = true) => new()
    {
        Provider = "Gmail",
        From = "sender@example.test",
        FromName = "Некрӯз Резиденс",
        Gmail = configured
            ? new EmailOptions.GmailSettings { ClientId = "test-client", ClientSecret = "test-secret", RefreshToken = "test-refresh" }
            : new EmailOptions.GmailSettings()
    };

    private sealed class StubGoogle : HttpMessageHandler
    {
        public int TokenCalls, SendCalls;
        public Queue<HttpStatusCode> SendResults { get; } = new();
        public string TokenError { get; set; } = "";
        public List<string> Raws { get; } = [];
        public List<string?> Bearers { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/token")
            {
                TokenCalls++;
                var form = await request.Content!.ReadAsStringAsync(ct);
                Assert.Contains("grant_type=refresh_token", form);
                Assert.Contains("refresh_token=test-refresh", form);
                if (TokenError.Length > 0)
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent($"{{\"error\":\"{TokenError}\"}}") };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"access_token\":\"access-{TokenCalls}\",\"expires_in\":3600,\"token_type\":\"Bearer\"}}")
                };
            }

            Assert.Equal("/gmail/v1/users/me/messages/send", request.RequestUri.AbsolutePath);
            SendCalls++;
            Bearers.Add(request.Headers.Authorization?.Parameter);
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
            Raws.Add(body.GetProperty("raw").GetString()!);
            var status = SendResults.Count > 0 ? SendResults.Dequeue() : HttpStatusCode.OK;
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private static (GmailEmailSender Sender, StubGoogle Google) Create(EmailOptions? o = null, GmailTokenCache? cache = null)
    {
        o ??= Options();
        o.Gmail.TokenUrl = "https://oauth.test/token";
        o.Gmail.ApiBaseUrl = "https://gmail.test";
        var google = new StubGoogle();
        var sender = new GmailEmailSender(new HttpClient(google), Microsoft.Extensions.Options.Options.Create(o), cache ?? new GmailTokenCache(),
            TimeProvider.System, NullLogger<GmailEmailSender>.Instance);
        return (sender, google);
    }

    [Fact]
    public async Task Token_IsRequestedOnce_AndCached()
    {
        var (sender, google) = Create();
        await sender.SendAsync(new EmailMessage("a@example.test", "Код 1", "Текст"));
        await sender.SendAsync(new EmailMessage("b@example.test", "Код 2", "Текст"));

        Assert.Equal(1, google.TokenCalls);
        Assert.Equal(2, google.SendCalls);
        Assert.All(google.Bearers, b => Assert.Equal("access-1", b));
    }

    [Fact]
    public async Task Unauthorized_RefreshesToken_AndRetriesOnce()
    {
        var (sender, google) = Create();
        google.SendResults.Enqueue(HttpStatusCode.Unauthorized);

        await sender.SendAsync(new EmailMessage("a@example.test", "Код", "Текст"));

        Assert.Equal(2, google.TokenCalls);
        Assert.Equal(["access-1", "access-2"], google.Bearers);
    }

    [Fact]
    public async Task OtherError_IsRussian_WithStatus_AndMaskedAddress()
    {
        var (sender, google) = Create();
        google.SendResults.Enqueue(HttpStatusCode.Forbidden);

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(new EmailMessage("buyer@example.test", "Код", "Текст")));

        Assert.Contains("Gmail не принял письмо", e.Message);
        Assert.Contains("403", e.Message);
        Assert.Contains("b***@example.test", e.Message);
        Assert.DoesNotContain("buyer@", e.Message);
    }

    [Fact]
    public async Task RevokedRefreshToken_ClearError_NoSecretsInMessage()
    {
        var (sender, google) = Create();
        google.TokenError = "invalid_grant";

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(new EmailMessage("a@example.test", "Код", "Текст")));

        Assert.Contains("Gmail не выдал токен доступа", e.Message);
        Assert.DoesNotContain("test-secret", e.Message);
        Assert.DoesNotContain("test-refresh", e.Message);
        Assert.Equal(0, google.SendCalls);
    }

    [Fact]
    public async Task NotConfigured_ClearError_NoRequests()
    {
        var (sender, google) = Create(Options(configured: false));

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(new EmailMessage("a@example.test", "Код", "Текст")));

        Assert.Equal(GmailEmailSender.NotConfigured, e.Message);
        Assert.Equal(0, google.TokenCalls + google.SendCalls);
    }

    [Fact]
    public async Task Raw_IsBase64Url_MimeWithUtf8Subject_AndBothParts()
    {
        var (sender, google) = Create();
        const string subject = "Некрӯз Резиденс: рамзи воридшавӣ 123456 — ҳ қ ғ ҷ ӣ ӯ, Кода для входа достаточно длинная тема";
        await sender.SendAsync(new EmailMessage("buyer@example.test", subject, "Ваш код: 123456 — ҳ қ ғ ҷ ӣ ӯ", "<p>Ваш код: <b>123456</b></p>"));

        var raw = Assert.Single(google.Raws);
        Assert.Matches("^[A-Za-z0-9_-]+$", raw);
        var mime = Encoding.UTF8.GetString(FromBase64Url(raw));
        var head = mime[..mime.IndexOf("\r\n\r\n", StringComparison.Ordinal)];

        // Тема: encoded-words UTF-8, вместе — исходная строка; строки заголовка не длиннее 78 символов.
        var subjectHeader = HeaderValue(head, "Subject");
        var decoded = string.Concat(EncodedWord().Matches(subjectHeader).Select(m => Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[1].Value))));
        Assert.Equal(subject, decoded);
        Assert.All(head.Split("\r\n"), line => Assert.True(line.Length <= 78, line));

        Assert.Contains("To: <buyer@example.test>", head);
        Assert.Contains("MIME-Version: 1.0", head);
        Assert.Contains("Message-ID: <", head);
        Assert.Matches(@"Date: \w{3}, \d{2} \w{3} \d{4} \d{2}:\d{2}:\d{2} \+0000", head);
        // Имя отправителя тоже в UTF-8.
        var fromName = EncodedWord().Matches(HeaderValue(head, "From")).Select(m => Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[1].Value)));
        Assert.Equal("Некрӯз Резиденс", string.Concat(fromName));
        Assert.Contains(" <sender@example.test>", HeaderValue(head, "From"));

        var boundary = Regex.Match(head, "boundary=\"([^\"]+)\"").Groups[1].Value;
        var parts = mime.Split("--" + boundary).Skip(1).Where(p => !p.StartsWith("--", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, parts.Count);
        Assert.Equal("Ваш код: 123456 — ҳ қ ғ ҷ ӣ ӯ", PartText(parts[0], "text/plain"));
        Assert.Equal("<p>Ваш код: <b>123456</b></p>", PartText(parts[1], "text/html"));
    }

    [Fact]
    public void HeaderInjection_NewLinesAreRemoved()
    {
        var mime = GmailEmailSender.BuildMime(Options(), new EmailMessage("a@example.test\r\nBcc: x@evil.test", "Тема\r\nBcc: y@evil.test", "Текст"), DateTimeOffset.UtcNow);
        var head = mime[..mime.IndexOf("\r\n\r\n", StringComparison.Ordinal)];
        Assert.DoesNotContain("\r\nBcc:", head);
    }

    private static string HeaderValue(string head, string name)
    {
        var lines = head.Split("\r\n");
        var i = Array.FindIndex(lines, l => l.StartsWith(name + ":", StringComparison.Ordinal));
        var value = new StringBuilder(lines[i][(name.Length + 1)..]);
        for (var j = i + 1; j < lines.Length && lines[j].StartsWith(' '); j++)
            value.Append(lines[j]);
        return value.ToString();
    }

    private static string PartText(string part, string type)
    {
        var split = part.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headers = part[..split];
        Assert.Contains($"Content-Type: {type}; charset=UTF-8", headers);
        Assert.Contains("Content-Transfer-Encoding: base64", headers);
        return Encoding.UTF8.GetString(Convert.FromBase64String(part[split..].Replace("\r\n", "")));
    }

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    [GeneratedRegex(@"=\?UTF-8\?B\?([A-Za-z0-9+/=]+)\?=")]
    private static partial Regex EncodedWord();
}

// /auth/methods и /health/ready с Email__Provider=Gmail вне Development (Staging, как Render).
public class GmailConfiguredFactory : StagingFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Email:Provider", "Gmail");
        builder.UseSetting("Email:Gmail:ClientId", "test-client");
        builder.UseSetting("Email:Gmail:ClientSecret", "test-secret");
        builder.UseSetting("Email:Gmail:RefreshToken", "test-refresh");
    }
}

public class GmailHalfConfiguredFactory : StagingFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Email:Provider", "Gmail");
        builder.UseSetting("Email:Gmail:ClientId", "test-client");
    }
}

public class GmailMethodsTests(GmailConfiguredFactory factory) : IClassFixture<GmailConfiguredFactory>
{
    [Fact]
    public async Task Gmail_WithAllThreeSettings_EmailWorks()
    {
        var client = factory.CreateClient();
        var methods = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.True(methods.GetProperty("email").GetBoolean());

        var ready = await client.GetFromJsonAsync<JsonElement>("/health/ready");
        var email = ready.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "email");
        Assert.Equal("Healthy", email.GetProperty("status").GetString());
        Assert.Equal("Почта: Gmail настроен", email.GetProperty("description").GetString());
    }
}

public class GmailHalfConfiguredTests(GmailHalfConfiguredFactory factory) : IClassFixture<GmailHalfConfiguredFactory>
{
    [Fact]
    public async Task Gmail_WithoutAllSettings_IsNotConfigured_503()
    {
        var client = factory.CreateClient();
        var methods = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.False(methods.GetProperty("email").GetBoolean());

        var send = await client.PostAsJsonAsync("/api/v1/auth/email/send", new { email = "buyer@example.test" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, send.StatusCode);
        Assert.Equal("email_not_configured", (await send.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var ready = await client.GetFromJsonAsync<JsonElement>("/health/ready");
        var email = ready.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "email");
        Assert.Equal("Degraded", email.GetProperty("status").GetString());
        Assert.StartsWith("Почта: Gmail не настроен", email.GetProperty("description").GetString());
    }
}

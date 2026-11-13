using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Access token Gmail в памяти процесса (синглтон): один на все письма, обновляется за 60 с до истечения.
// SemaphoreSlim — одновременные письма не запрашивают токен по нескольку раз.
public sealed class GmailTokenCache
{
    internal readonly SemaphoreSlim Lock = new(1, 1);
    internal string? Token;
    internal DateTimeOffset ExpiresAt;

    public void Reset()
    {
        Token = null;
        ExpiresAt = DateTimeOffset.MinValue;
    }
}

// Письма через Gmail API (HTTPS): refresh token → access token → users.me.messages.send с письмом в MIME (base64url).
// Письмо собирается вручную, без внешних пакетов: тема и имя отправителя — encoded-word UTF-8 (кириллица, ӯ ҳ қ ғ ҷ ӣ),
// тело — multipart/alternative из text/plain и, если есть, text/html, обе части в base64.
// В лог и в исключения не попадают ни токены, ни ответ Google на запрос токена, ни адрес получателя целиком.
public class GmailEmailSender(
    HttpClient http,
    IOptions<EmailOptions> options,
    GmailTokenCache cache,
    TimeProvider time,
    ILogger<GmailEmailSender> logger) : IEmailSender
{
    public const string NotConfigured =
        "Gmail не настроен: задайте Email__Gmail__ClientId, Email__Gmail__ClientSecret и Email__Gmail__RefreshToken";
    private const string RefreshRevoked = "Refresh token Gmail отозван или истёк — получите новый (backend/scripts/gmail-token.mjs)";
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(60);

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var o = options.Value;
        if (!o.Gmail.IsConfigured)
            throw new InvalidOperationException(NotConfigured);

        var raw = Base64Url(Encoding.UTF8.GetBytes(BuildMime(o, message, time.GetUtcNow())));
        var url = new Uri(new Uri(o.Gmail.ApiBaseUrl), "/gmail/v1/users/me/messages/send");

        // 401 — токен отозван раньше срока: один раз берём новый и повторяем.
        for (var attempt = 0; ; attempt++)
        {
            var token = await GetTokenAsync(o.Gmail, ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { raw }) };
            request.Headers.Authorization = new("Bearer", token);
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return;
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                cache.Reset();
                continue;
            }
            throw new InvalidOperationException(
                $"Gmail не принял письмо для {MaskEmail(message.To)}: HTTP {(int)response.StatusCode}");
        }
    }

    private async Task<string> GetTokenAsync(EmailOptions.GmailSettings g, CancellationToken ct)
    {
        if (cache.Token is { } fresh && time.GetUtcNow() < cache.ExpiresAt)
            return fresh;

        await cache.Lock.WaitAsync(ct);
        try
        {
            if (cache.Token is { } other && time.GetUtcNow() < cache.ExpiresAt)
                return other;

            using var response = await http.PostAsync(g.TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = g.ClientId!,
                ["client_secret"] = g.ClientSecret!,
                ["refresh_token"] = g.RefreshToken!,
                ["grant_type"] = "refresh_token"
            }), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                // Ответ Google на запрос токена в лог не пишется целиком: берём только код ошибки.
                if (ErrorCode(body) == "invalid_grant")
                    logger.LogError("{Reason}", RefreshRevoked);
                throw new InvalidOperationException($"Gmail не выдал токен доступа: HTTP {(int)response.StatusCode}");
            }

            using var json = JsonDocument.Parse(body);
            var token = json.RootElement.GetProperty("access_token").GetString()
                        ?? throw new InvalidOperationException("Gmail не выдал токен доступа: пустой ответ");
            var seconds = json.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var s) ? s : 3600;
            cache.Token = token;
            cache.ExpiresAt = time.GetUtcNow().AddSeconds(seconds) - Margin;
            return token;
        }
        finally
        {
            cache.Lock.Release();
        }
    }

    private static string? ErrorCode(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---------- MIME ----------

    public static string BuildMime(EmailOptions o, EmailMessage message, DateTimeOffset now)
    {
        var to = NoNewLines(message.To);
        var from = NoNewLines(o.From);
        var boundary = "=_binosoz_" + Guid.NewGuid().ToString("N");
        var sb = new StringBuilder();
        // Длинные заголовки переносятся (folding: перевод строки и пробел) — строка не длиннее 78 символов.
        sb.Append("From: ").Append(DisplayName(o.FromName)).Append("\r\n <").Append(from).Append(">\r\n");
        sb.Append("To: <").Append(to).Append(">\r\n");
        sb.Append("Subject: ").Append(EncodedWords(message.Subject)).Append("\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append("Date: ").Append(now.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss '+0000'", CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("Message-ID: <").Append(Guid.NewGuid().ToString("N")).Append('@').Append(Domain(from)).Append(">\r\n");
        sb.Append("Content-Type: multipart/alternative;\r\n boundary=\"").Append(boundary).Append("\"\r\n\r\n");
        AppendPart(sb, boundary, "text/plain", message.Text);
        if (message.Html is not null)
            AppendPart(sb, boundary, "text/html", message.Html);
        sb.Append("--").Append(boundary).Append("--\r\n");
        return sb.ToString();
    }

    private static void AppendPart(StringBuilder sb, string boundary, string type, string content)
    {
        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Type: ").Append(type).Append("; charset=UTF-8\r\n");
        sb.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        for (var i = 0; i < b64.Length; i += 76)
            sb.Append(b64, i, Math.Min(76, b64.Length - i)).Append("\r\n");
    }

    // Имя отправителя: ASCII — в кавычках, иначе encoded-word.
    private static string DisplayName(string name)
    {
        name = NoNewLines(name);
        return name.All(c => c < 128) ? "\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : EncodedWords(name);
    }

    // RFC 2047: =?UTF-8?B?…?= кусками не длиннее 39 байт (с именем заголовка строка ≤ 76 символов), символы не разрываются.
    public static string EncodedWords(string text)
    {
        text = NoNewLines(text);
        var words = new List<string>();
        var chunk = new StringBuilder();
        var bytes = 0;
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
        {
            var element = (string)e.Current;
            var n = Encoding.UTF8.GetByteCount(element);
            if (bytes + n > 39 && chunk.Length > 0)
            {
                words.Add(Word(chunk.ToString()));
                chunk.Clear();
                bytes = 0;
            }
            chunk.Append(element);
            bytes += n;
        }
        if (chunk.Length > 0 || words.Count == 0)
            words.Add(Word(chunk.ToString()));
        return string.Join("\r\n ", words);

        static string Word(string s) => "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(s)) + "?=";
    }

    public static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // Перевод строки в адресе или теме — попытка вставить свой заголовок: убираем.
    private static string NoNewLines(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();

    private static string Domain(string address) => address.Contains('@') ? address[(address.LastIndexOf('@') + 1)..] : "binosoz.local";

    // a***@example.com — для текста ошибок и логов.
    public static string MaskEmail(string address)
    {
        var at = address.IndexOf('@');
        return at <= 0 ? "***" : address[0] + "***" + address[at..];
    }
}

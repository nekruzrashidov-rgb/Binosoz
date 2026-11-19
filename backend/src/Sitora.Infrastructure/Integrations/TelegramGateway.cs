using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sitora.Infrastructure.Integrations;

// Telegram Gateway (gatewayapi.telegram.org): код входа приходит в Telegram на номер телефона, бот не нужен.
// Платно (около $0.01 за доставленный код, за недоставленные деньги возвращаются). Токен — Telegram__Gateway__Token;
// без него канал выключен. Суточный лимит отправок — Telegram__Gateway__DailyLimit (по умолчанию 200).
public class TelegramGatewayOptions
{
    public string? Token { get; set; }
    public int DailyLimit { get; set; } = 200;
    public string BaseUrl { get; set; } = "https://gatewayapi.telegram.org";

    // Необязательно: адрес для отчётов о доставке (POST /api/v1/telegram/gateway/report на этом API).
    public string? CallbackUrl { get; set; }

    public bool Configured => !string.IsNullOrWhiteSpace(Token);
}

public enum GatewaySendStatus
{
    Sent,
    // У номера нет Telegram (или Telegram не может доставить на него код).
    NoTelegram,
    Failed
}

public record GatewaySendResult(GatewaySendStatus Status, string? RequestId = null, string? Error = null);

public interface ITelegramGateway
{
    bool Configured { get; }

    // Наш 6-значный код на номер: checkSendAbility (можно ли доставить) → sendVerificationMessage с тем же request_id
    // (повторная проверка не оплачивается) и ttl.
    Task<GatewaySendResult> SendCodeAsync(string phone, string code, int ttlSeconds, CancellationToken ct = default);

    // Сообщить Telegram, что код введён верно (статистика доставки). Сбой не мешает входу.
    Task CheckStatusAsync(string requestId, string code, CancellationToken ct = default);

    // Подпись отчёта о доставке: HMAC-SHA256(ключ = SHA256(токен), данные = timestamp + "\n" + тело).
    bool IsValidReport(string? timestamp, string? signature, string body);
}

public sealed class TelegramGatewayClient(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramGatewayClient> logger) : ITelegramGateway
{
    public const string HttpClientName = "telegram-gateway";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private TelegramGatewayOptions O => options.Value.Gateway;

    public bool Configured => O.Configured;

    public async Task<GatewaySendResult> SendCodeAsync(string phone, string code, int ttlSeconds, CancellationToken ct = default)
    {
        try
        {
            var ability = await PostAsync("checkSendAbility", new { PhoneNumber = phone }, ct);
            if (!ability.Ok)
                return IsPhoneProblem(ability.Error)
                    ? new GatewaySendResult(GatewaySendStatus.NoTelegram, null, ability.Error)
                    : new GatewaySendResult(GatewaySendStatus.Failed, null, ability.Error);

            var sent = await PostAsync("sendVerificationMessage", new
            {
                PhoneNumber = phone,
                RequestId = ability.Result?.RequestId,
                Code = code,
                Ttl = ttlSeconds,
                CallbackUrl = string.IsNullOrWhiteSpace(O.CallbackUrl) ? null : O.CallbackUrl
            }, ct);
            if (!sent.Ok)
                return IsPhoneProblem(sent.Error)
                    ? new GatewaySendResult(GatewaySendStatus.NoTelegram, null, sent.Error)
                    : new GatewaySendResult(GatewaySendStatus.Failed, null, sent.Error);
            return new GatewaySendResult(GatewaySendStatus.Sent, sent.Result?.RequestId ?? ability.Result?.RequestId);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Telegram Gateway не ответил");
            return new GatewaySendResult(GatewaySendStatus.Failed, null, e.GetType().Name);
        }
    }

    public async Task CheckStatusAsync(string requestId, string code, CancellationToken ct = default)
    {
        try
        {
            var r = await PostAsync("checkVerificationStatus", new { RequestId = requestId, Code = code }, ct);
            if (!r.Ok)
                logger.LogInformation("Telegram Gateway: checkVerificationStatus — {Error}", r.Error);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation(e, "Telegram Gateway: статус кода не отправлен");
        }
    }

    public bool IsValidReport(string? timestamp, string? signature, string body)
    {
        if (!Configured || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
            return false;
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(O.Token!));
        var expected = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(timestamp + "\n" + body))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    // Ошибки про сам номер: неверный, нет Telegram, нельзя доставить.
    private static bool IsPhoneProblem(string? error) =>
        error is not null && (error.Contains("PHONE", StringComparison.OrdinalIgnoreCase) || error.Contains("USER", StringComparison.OrdinalIgnoreCase));

    private record GatewayResponse(bool Ok, string? Error, GatewayStatus? Result);

    private record GatewayStatus(string? RequestId);

    private async Task<GatewayResponse> PostAsync(string method, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{O.BaseUrl.TrimEnd('/')}/{method}")
        {
            Content = JsonContent.Create(body, options: Json)
        };
        request.Headers.Authorization = new("Bearer", O.Token);
        using var response = await http.SendAsync(request, ct);
        var parsed = await response.Content.ReadFromJsonAsync<GatewayResponse>(Json, ct);
        return parsed ?? new GatewayResponse(false, $"HTTP {(int)response.StatusCode}", null);
    }
}

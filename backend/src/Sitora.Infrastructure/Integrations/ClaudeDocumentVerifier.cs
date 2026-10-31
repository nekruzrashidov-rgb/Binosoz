using System.Globalization;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Чтение паспортов и банковских чеков моделью Claude с распознаванием изображений (официальный SDK).
// Изображение (JPG, PNG, GIF, WebP до 5 МБ) или PDF (до 10 МБ) уходит в base64 вместе с короткой инструкцией;
// модель отвечает JSON, который разбирается здесь. Нечитаемый ответ — документ «не прочитан», а не ошибка.
public sealed class ClaudeDocumentVerifier(IOptions<ClaudeOptions> options, TimeProvider timeProvider, ILogger<ClaudeDocumentVerifier> logger)
    : IDocumentVerifier
{
    public const long MaxImageSize = 5 * 1024 * 1024;
    public const long MaxPdfSize = 10 * 1024 * 1024;
    public const string NotConfigured = "Проверка документов ИИ не настроена";
    public const string Unreadable = "Не удалось прочитать документ";
    private const string Unavailable = "Проверка документов ИИ временно недоступна, попробуйте позже";
    private static readonly string[] ImageTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];

    private const string PassportSystem = "Ты проверяешь документы удостоверяющие личность. Отвечай ТОЛЬКО в JSON.";
    private const string ReceiptSystem = "Ты читаешь банковские квитанции. Отвечай ТОЛЬКО в JSON.";

    private AnthropicClient? _client;

    public async Task<DocumentVerifyResult> VerifyPassportAsync(Stream imageStream, string contentType, string expectedFullName, CancellationToken ct = default)
    {
        var prompt = $"Проверь документ. Ожидаемое ФИО: {expectedFullName}\n" +
                     "JSON: {extractedFullName, expiryDate (YYYY-MM-DD), nameMatches, isExpired, isReadable, issue}";
        var raw = await AskAsync(PassportSystem, prompt, imageStream, contentType, ct);
        return ParsePassport(raw, Today);
    }

    public async Task<ReceiptReadResult> ReadReceiptAsync(Stream imageStream, string contentType, CancellationToken ct = default)
    {
        const string prompt = "Прочитай чек.\nJSON: {amount (число), currency (TJS/USD/RUB), paymentDate (YYYY-MM-DD), description, bankName, isReadable}";
        var raw = await AskAsync(ReceiptSystem, prompt, imageStream, contentType, ct);
        return ParseReceipt(raw);
    }

    private async Task<string> AskAsync(string system, string prompt, Stream file, string contentType, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey))
            throw new ServiceUnavailableException(NotConfigured);

        var type = contentType.Split(';')[0].Trim().ToLowerInvariant();
        var isPdf = type == "application/pdf";
        if (!isPdf && !ImageTypes.Contains(type))
            throw new BusinessRuleException("Документ должен быть изображением JPG, PNG, GIF, WebP или PDF");

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        if (buffer.Length == 0)
            throw new BusinessRuleException("Файл пустой");
        if (buffer.Length > (isPdf ? MaxPdfSize : MaxImageSize))
            throw new BusinessRuleException(isPdf ? "PDF — не больше 10 МБ" : "Изображение — не больше 5 МБ");
        var data = Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);

        // Документ — перед текстом инструкции.
        ContentBlockParam document = isPdf
            ? new DocumentBlockParam { Source = new Base64PdfSource { Data = data } }
            : new ImageBlockParam { Source = new Base64ImageSource { Data = data, MediaType = type } };

        _client ??= new AnthropicClient { ApiKey = o.ApiKey };
        Message response;
        try
        {
            response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = o.VisionModel,
                MaxTokens = o.VisionMaxTokens,
                System = system,
                // Извлечение полей: глубокие рассуждения не нужны.
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = [new UserMessageParam { Content = new List<ContentBlockParam> { document, new TextBlockParam { Text = prompt } } }],
            }, ct);
        }
        catch (AnthropicRateLimitException e)
        {
            logger.LogWarning(e, "Claude: превышен лимит запросов при чтении документа");
            throw new ServiceUnavailableException(Unavailable);
        }
        catch (AnthropicApiException e)
        {
            logger.LogError(e, "Claude не прочитал документ");
            throw new ServiceUnavailableException(Unavailable);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Нет связи с Claude");
            throw new ServiceUnavailableException(Unavailable);
        }

        // Отказ или обрезанный ответ — пустая строка: разбор отметит документ как непрочитанный.
        if (response.StopReason == "refusal" || response.StopReason == "max_tokens")
        {
            logger.LogWarning("Claude не дочитал документ: {StopReason}", response.StopReason);
            return "";
        }
        return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
    }

    // ---------- Разбор ответа (отдельно, чтобы проверять без обращения к модели) ----------

    // Своей проверкой поверх ответа модели: срок сверяется с сегодняшней датой, а не только со словами модели.
    public static DocumentVerifyResult ParsePassport(string raw, DateOnly today)
    {
        if (!TryParseObject(raw, out var json))
            return new DocumentVerifyResult(false, null, null, false, false, Unreadable, raw);

        var readable = Bool(json, "isReadable") ?? true;
        var name = String(json, "extractedFullName");
        var expiry = Date(json, "expiryDate");
        var nameMatches = Bool(json, "nameMatches") ?? false;
        var expired = expiry is { } date ? date < today : Bool(json, "isExpired") ?? false;
        var issue = String(json, "issue");
        if (!readable)
            return new DocumentVerifyResult(false, name, expiry, false, expired, issue ?? Unreadable, raw);

        issue ??= !nameMatches ? "ФИО не совпадает" : expired ? "Срок действия истёк" : null;
        return new DocumentVerifyResult(nameMatches && !expired, name, expiry, nameMatches, expired, issue, raw);
    }

    public static ReceiptReadResult ParseReceipt(string raw)
    {
        if (!TryParseObject(raw, out var json))
            return new ReceiptReadResult(false, null, null, null, null, null, raw);

        var amount = Decimal(json, "amount");
        var currency = String(json, "currency")?.ToUpperInvariant();
        var readable = (Bool(json, "isReadable") ?? true) && amount is > 0;
        return new ReceiptReadResult(readable, amount, currency, Date(json, "paymentDate"), String(json, "description"), String(json, "bankName"), raw);
    }

    // Модель может обернуть JSON в ```json … ``` или добавить слова: берём первый объект целиком.
    private static bool TryParseObject(string raw, out JsonElement json)
    {
        json = default;
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;
        try
        {
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            json = doc.RootElement.Clone();
            return json.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement? Get(JsonElement json, string name) =>
        json.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } p
            ? p.Value
            : null;

    private static string? String(JsonElement json, string name) =>
        Get(json, name) is { ValueKind: JsonValueKind.String } v && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;

    private static bool? Bool(JsonElement json, string name) => Get(json, name) switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        { ValueKind: JsonValueKind.String } v => bool.TryParse(v.GetString(), out var b) ? b : null,
        _ => null
    };

    private static decimal? Decimal(JsonElement json, string name) => Get(json, name) switch
    {
        { ValueKind: JsonValueKind.Number } v => v.GetDecimal(),
        { ValueKind: JsonValueKind.String } v => decimal.TryParse(v.GetString()?.Replace(" ", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null,
        _ => null
    };

    private static DateOnly? Date(JsonElement json, string name) =>
        String(json, name) is { } s && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private DateOnly Today => Sitora.Domain.Common.DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
}

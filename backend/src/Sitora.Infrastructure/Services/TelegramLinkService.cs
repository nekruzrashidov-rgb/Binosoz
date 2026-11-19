using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Привязка Telegram без ручного chat id: кабинет выдаёт ссылку t.me/<бот>?start=<токен> (одноразовый, 15 минут),
// бот получает «/start <токен>» через webhook и сохраняет chat_id у покупателя. «/stop» — отвязать этот чат.
public class TelegramLinkService(
    AppDbContext db,
    IDistributedCache cache,
    ITelegramNotifier telegram,
    IOptions<TelegramOptions> options,
    IAuditService audit,
    TimeProvider timeProvider,
    ILogger<TelegramLinkService> logger)
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);
    public const string NotConfigured = "Бот Telegram не настроен: уведомления в Telegram пока недоступны";
    private const string CachePrefix = "tg-link:";

    private record LinkTicket(Guid UserId, string Language, DateTime ExpiresAt);

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<TelegramLinkResponse> CreateLinkAsync(Guid userId, string language, CancellationToken ct = default)
    {
        var o = options.Value;
        if (!o.BotConfigured)
            throw new ServiceUnavailableException(NotConfigured);
        // 24 случайных байта → 32 символа [A-Za-z0-9_-]: так разрешает параметр start (до 64 символов).
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        var ticket = new LinkTicket(userId, BuyerTexts.Lang(language), Now.Add(LinkLifetime));
        await cache.SetAsync(CachePrefix + Hash(token), JsonSerializer.SerializeToUtf8Bytes(ticket),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = LinkLifetime }, ct);
        return new TelegramLinkResponse($"https://t.me/{o.BotUsername!.TrimStart('@')}?start={token}", ticket.ExpiresAt);
    }

    public async Task UnlinkAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("Пользователь не найден");
        if (user.TelegramChatId is null)
            return;
        user.TelegramChatId = null;
        audit.Record("User", user.Id, "TelegramUnlinked", null, new { By = "cabinet" });
        await db.SaveChangesAsync(ct);
    }

    // Обновление от Telegram (webhook). Отвечаем только в личных чатах; ошибки не бросаем — Telegram повторял бы запрос.
    public async Task HandleUpdateAsync(JsonElement update, CancellationToken ct = default)
    {
        if (!update.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            return;
        if (!message.TryGetProperty("chat", out var chat) || !chat.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var chatId))
            return;
        if (chat.TryGetProperty("type", out var type) && type.GetString() != "private")
            return;
        var text = message.TryGetProperty("text", out var t) ? t.GetString()?.Trim() ?? "" : "";
        var lang = message.TryGetProperty("from", out var from) && from.TryGetProperty("language_code", out var lc)
            ? BuyerTexts.Lang(lc.GetString()?.Split('-')[0]) : BuyerTexts.Ru;

        var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var name = command.Length > 0 ? command[0].Split('@')[0].ToLowerInvariant() : "";
        if (name == "/start" && command.Length == 2)
            await LinkAsync(chatId, command[1].Trim(), lang, ct);
        else if (name == "/stop")
            await StopAsync(chatId, lang, ct);
        else
            await telegram.SendAsync(chatId, BuyerTexts.BotHello(lang), ct);
    }

    private async Task LinkAsync(long chatId, string token, string lang, CancellationToken ct)
    {
        var key = CachePrefix + Hash(token);
        var bytes = token.Length is > 10 and <= 64 ? await cache.GetAsync(key, ct) : null;
        var ticket = bytes is null ? null : JsonSerializer.Deserialize<LinkTicket>(bytes);
        var user = ticket is null || ticket.ExpiresAt <= Now ? null : await db.Users.FirstOrDefaultAsync(u => u.Id == ticket.UserId && u.IsActive, ct);
        if (user is null)
        {
            await telegram.SendAsync(chatId, BuyerTexts.BotBadLink(lang), ct);
            return;
        }
        // Ссылка одноразовая: сразу удаляем.
        await cache.RemoveAsync(key, ct);
        user.TelegramChatId = chatId;
        user.NotifyByTelegram = true;
        user.NotifyLanguage ??= ticket!.Language;
        audit.Record("User", user.Id, "TelegramLinked", null, new { By = "bot" });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Telegram привязан к пользователю {UserId}", user.Id);

        var brand = await db.Customers.IgnoreQueryFilters().Where(c => c.UserId == user.Id)
            .Select(c => db.Organizations.IgnoreQueryFilters().Where(o => o.Id == c.OrganizationId).Select(o => o.Name).FirstOrDefault())
            .FirstOrDefaultAsync(ct)
            ?? await db.Organizations.IgnoreQueryFilters().Where(o => o.Id == user.OrganizationId).Select(o => o.Name).FirstOrDefaultAsync(ct)
            ?? "";
        await telegram.SendAsync(chatId, BuyerTexts.BotLinked(BuyerTexts.Lang(user.NotifyLanguage ?? ticket!.Language), brand.Trim()), ct);
    }

    private async Task StopAsync(long chatId, string lang, CancellationToken ct)
    {
        var users = await db.Users.Where(u => u.TelegramChatId == chatId).ToListAsync(ct);
        foreach (var u in users)
        {
            u.TelegramChatId = null;
            audit.Record("User", u.Id, "TelegramUnlinked", null, new { By = "bot" });
        }
        await db.SaveChangesAsync(ct);
        await telegram.SendAsync(chatId, BuyerTexts.BotStopped(lang), ct);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

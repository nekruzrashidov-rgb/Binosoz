using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

// Очередь отправки сообщений покупателю: запись BuyerMessage уже в базе, задача только отправляет её.
public interface IBuyerMessageQueue
{
    void Enqueue(Guid messageId);
}

public class HangfireBuyerMessageQueue(IBackgroundJobClient jobs) : IBuyerMessageQueue
{
    public void Enqueue(Guid messageId) => jobs.Enqueue<BuyerMessageJob>(job => job.SendAsync(messageId, CancellationToken.None));
}

// Сбой отправки, который стоит повторить: Hangfire перезапустит задачу через паузу.
public class BuyerMessageRetryException(string message) : Exception(message);

// Отправляет одно сообщение (Telegram или письмо) и пишет итог в журнал: Sent, Skipped (канал отключён, бот
// заблокирован — тогда чат отвязывается), Failed (после MaxAttempts попыток). Временный сбой — исключение, Hangfire
// повторит через 1, 5, 15 и 60 минут.
public class BuyerMessageJob(
    AppDbContext db,
    ITelegramNotifier telegram,
    IEmailSender email,
    TimeProvider timeProvider,
    ILogger<BuyerMessageJob> logger)
{
    [AutomaticRetry(Attempts = BuyerMessage.MaxAttempts - 1, DelaysInSeconds = [60, 300, 900, 3600], OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SendAsync(Guid messageId, CancellationToken ct)
    {
        var m = await db.BuyerMessages.IgnoreQueryFilters().Include(x => x.User).FirstOrDefaultAsync(x => x.Id == messageId, ct);
        if (m is null || m.Status != BuyerMessageStatus.Pending)
            return;
        m.Attempts++;

        var (status, error) = m.Channel switch
        {
            BuyerChannels.Telegram => await TelegramAsync(m, ct),
            BuyerChannels.Email => await EmailAsync(m, ct),
            _ => (BuyerMessageStatus.Sent, null)
        };

        if (status == BuyerMessageStatus.Failed && m.Attempts < BuyerMessage.MaxAttempts)
        {
            m.Error = error;
            await db.SaveChangesAsync(ct);
            throw new BuyerMessageRetryException($"Сообщение {m.Id} ({m.Channel}) не отправлено, будет повтор: {error}");
        }

        m.Status = status;
        m.Error = error;
        if (status == BuyerMessageStatus.Sent)
            m.SentAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    private async Task<(string, string?)> TelegramAsync(BuyerMessage m, CancellationToken ct)
    {
        var user = m.User;
        if (user.TelegramChatId is not { } chat || !user.NotifyByTelegram || !user.IsActive)
            return (BuyerMessageStatus.Skipped, "Telegram не привязан или отключён");
        switch (await telegram.TrySendAsync(chat, m.Text, ct))
        {
            case TelegramSendResult.Sent:
                return (BuyerMessageStatus.Sent, null);
            case TelegramSendResult.Blocked:
                // Бот заблокирован или чат удалён: отвязываем, дальше туда не шлём.
                user.TelegramChatId = null;
                logger.LogInformation("Покупатель {UserId} заблокировал бота: Telegram отвязан", user.Id);
                return (BuyerMessageStatus.Skipped, "Бот заблокирован — Telegram отвязан");
            case TelegramSendResult.NotConfigured:
                return (BuyerMessageStatus.Skipped, "Бот Telegram не настроен");
            default:
                return (BuyerMessageStatus.Failed, "Telegram не ответил");
        }
    }

    private async Task<(string, string?)> EmailAsync(BuyerMessage m, CancellationToken ct)
    {
        var user = m.User;
        if (string.IsNullOrWhiteSpace(user.Email) || !user.EmailConfirmed || !user.NotifyByEmail || !user.IsActive)
            return (BuyerMessageStatus.Skipped, "Почта не подтверждена или письма отключены");
        try
        {
            var fromName = m.OrganizationId is { } org
                ? await db.Organizations.IgnoreQueryFilters().Where(o => o.Id == org).Select(o => o.Name).FirstOrDefaultAsync(ct)
                : null;
            await email.SendAsync(new EmailMessage(user.Email, m.Subject ?? "", m.Text, m.Html, fromName), ct);
            return (BuyerMessageStatus.Sent, null);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Письмо покупателю {UserId} не отправлено", user.Id);
            return (BuyerMessageStatus.Failed, Shorten(e.Message));
        }
    }

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..300];
}

// Ссылка «Отписаться» в письме: без входа, подпись HMAC (ключ — из Jwt:Key), 90 дней. Повторный переход ничего
// не меняет: письма уже выключены. Включить снова — в кабинете.
public class UnsubscribeTokens(IOptions<JwtOptions> jwt, TimeProvider timeProvider)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(90);

    public string Create(Guid userId)
    {
        var expires = timeProvider.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds();
        var payload = $"{userId:N}.{expires}";
        return $"{payload}.{Sign(payload)}";
    }

    public bool TryRead(string? token, out Guid userId)
    {
        userId = Guid.Empty;
        var parts = token?.Split('.') ?? [];
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var id) || !long.TryParse(parts[1], out var expires))
            return false;
        var expected = Sign($"{parts[0]}.{parts[1]}");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2])))
            return false;
        if (DateTimeOffset.FromUnixTimeSeconds(expires) < timeProvider.GetUtcNow())
            return false;
        userId = id;
        return true;
    }

    private string Sign(string payload)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("sitora-unsubscribe:" + jwt.Value.Key));
        return Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));
    }
}

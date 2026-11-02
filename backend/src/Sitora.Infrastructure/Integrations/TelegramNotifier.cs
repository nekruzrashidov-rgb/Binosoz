using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace Sitora.Infrastructure.Integrations;

// Настройки бота (раздел Telegram; в разработке — user secrets Telegram:BotToken, на сервере — Telegram__BotToken).
public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string? BotToken { get; set; }

    // Повторы при сбое сети: сколько раз повторить и с какой паузой.
    public int Retries { get; set; } = 2;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}

// Сам вызов Bot API — отдельно, чтобы повторы и пропуски проверялись тестами без сети.
public interface ITelegramTransport
{
    Task SendAsync(string token, long chatId, string markdown, CancellationToken ct);
}

public sealed class TelegramBotTransport(IHttpClientFactory httpClients) : ITelegramTransport
{
    public const string HttpClientName = "telegram";

    public async Task SendAsync(string token, long chatId, string markdown, CancellationToken ct)
    {
        var bot = new TelegramBotClient(token, httpClients.CreateClient(HttpClientName));
        await bot.SendMessage(chatId, markdown, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
}

// Сообщение в личный чат Telegram. Уведомление — дополнение к колокольчику: без токена, без chatId
// или при сбое Telegram ничего не падает, только пишется в лог.
public sealed class TelegramNotifier(IOptions<TelegramOptions> options, ITelegramTransport transport, ILogger<TelegramNotifier> logger) : ITelegramNotifier
{
    public async Task SendAsync(long chatId, string message, CancellationToken ct = default)
    {
        if (chatId == 0)
            return;
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.BotToken))
        {
            logger.LogWarning("Telegram не настроен (нет Telegram:BotToken), сообщение не отправлено");
            return;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await transport.SendAsync(o.BotToken, chatId, message, ct);
                return;
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                if (attempt >= o.Retries || !IsTransient(e))
                {
                    logger.LogWarning(e, "Не удалось отправить сообщение в Telegram (чат {ChatId})", chatId);
                    return;
                }
                logger.LogInformation("Telegram не ответил, повтор через {Delay}", o.RetryDelay);
                await Task.Delay(o.RetryDelay, ct);
            }
        }
    }

    // Повторяем сбои сети и временные ошибки Telegram; неверный чат или заблокированный бот повтор не исправит.
    private static bool IsTransient(Exception e) => e switch
    {
        ApiRequestException api => api.ErrorCode is 429 or >= 500,
        RequestException or HttpRequestException or TaskCanceledException => true,
        _ => false
    };
}

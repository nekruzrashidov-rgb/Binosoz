using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Integrations;

// Заглушка: в Telegram ничего не уходит. Сообщение пишется в лог приложения
// и в таблицу Notifications. Настоящую отправку через Bot API добавим, когда будет токен бота.
public class TelegramNotificationSender(
    AppDbContext db,
    TimeProvider timeProvider,
    ILogger<TelegramNotificationSender> logger) : INotificationSender
{
    public const string Channel = "Telegram";

    public async Task SendAsync(Guid? userId, string text, CancellationToken ct = default)
    {
        logger.LogInformation("[Telegram, заглушка] Получатель {Recipient}: {Text}",
            userId?.ToString() ?? "канал менеджеров", text);

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Channel = Channel,
            Text = text,
            SentAt = timeProvider.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Integrations;

// Уведомление в колокольчик (таблица Notifications, канал Telegram) и строка в лог.
// Сообщения в сам Telegram отправляет TelegramAlertService через ITelegramNotifier тем, кто привязал чат.
public class TelegramNotificationSender(
    AppDbContext db,
    TimeProvider timeProvider,
    ILogger<TelegramNotificationSender> logger) : INotificationSender
{
    public const string Channel = "Telegram";

    public async Task SendAsync(Guid? userId, string text, CancellationToken ct = default)
    {
        logger.LogInformation("[Колокольчик] Получатель {Recipient}: {Text}",
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

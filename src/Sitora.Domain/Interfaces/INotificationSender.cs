namespace Sitora.Domain.Interfaces;

public interface INotificationSender
{
    // userId = null — общий канал менеджеров.
    Task SendAsync(Guid? userId, string text, CancellationToken ct = default);
}

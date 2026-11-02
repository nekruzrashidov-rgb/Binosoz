using System.Collections.Concurrent;
using Sitora.Domain.Interfaces;

namespace Sitora.Tests.Infrastructure;

// Вместо Telegram: тесты не ходят в сеть, отправленные сообщения складываются сюда.
public class FakeTelegramNotifier : ITelegramNotifier
{
    public ConcurrentQueue<(long ChatId, string Text)> Sent { get; } = new();

    public IReadOnlyList<string> To(long chatId) => Sent.Where(m => m.ChatId == chatId).Select(m => m.Text).ToList();

    public Task SendAsync(long chatId, string message, CancellationToken ct = default)
    {
        Sent.Enqueue((chatId, message));
        return Task.CompletedTask;
    }
}

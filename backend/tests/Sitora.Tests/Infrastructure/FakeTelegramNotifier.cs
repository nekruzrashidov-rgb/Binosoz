using System.Collections.Concurrent;
using Sitora.Domain.Interfaces;

namespace Sitora.Tests.Infrastructure;

// Вместо Telegram: тесты не ходят в сеть, отправленные сообщения складываются сюда.
// Blocked — чаты, где «пользователь заблокировал бота» (403); FailNext — столько следующих отправок не пройдут (сбой сети).
public class FakeTelegramNotifier : ITelegramNotifier
{
    public ConcurrentQueue<(long ChatId, string Text)> Sent { get; } = new();
    public ConcurrentDictionary<long, bool> Blocked { get; } = new();
    private int _failNext;

    public void FailNext(int count) => Interlocked.Exchange(ref _failNext, count);

    public IReadOnlyList<string> To(long chatId) => Sent.Where(m => m.ChatId == chatId).Select(m => m.Text).ToList();

    public Task SendAsync(long chatId, string message, CancellationToken ct = default)
    {
        Sent.Enqueue((chatId, message));
        return Task.CompletedTask;
    }

    public Task<TelegramSendResult> TrySendAsync(long chatId, string message, CancellationToken ct = default)
    {
        if (Blocked.ContainsKey(chatId))
            return Task.FromResult(TelegramSendResult.Blocked);
        if (Interlocked.Decrement(ref _failNext) >= 0)
            return Task.FromResult(TelegramSendResult.Failed);
        Interlocked.Exchange(ref _failNext, 0);
        Sent.Enqueue((chatId, message));
        return Task.FromResult(TelegramSendResult.Sent);
    }
}

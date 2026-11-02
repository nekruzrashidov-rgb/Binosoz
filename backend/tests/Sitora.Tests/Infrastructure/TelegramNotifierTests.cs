using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sitora.Infrastructure.Integrations;
using Telegram.Bot.Exceptions;

namespace Sitora.Tests.Infrastructure;

// Отправитель без сети: вызов Bot API подменён, проверяются пропуски и повторы.
public class TelegramNotifierTests
{
    [Fact]
    public async Task Send_PassesTokenChatAndMarkdown()
    {
        var transport = new FakeTransport();

        await Notifier(transport, "bot-token").SendAsync(12345, "💰 *Новый платёж*");

        Assert.Equal([("bot-token", 12345L, "💰 *Новый платёж*")], transport.Calls);
    }

    [Fact]
    public async Task NoToken_DoesNotThrow_AndSendsNothing()
    {
        var transport = new FakeTransport();

        await Notifier(transport, token: null).SendAsync(12345, "текст");
        await Notifier(transport, token: "  ").SendAsync(12345, "текст");

        Assert.Empty(transport.Calls);
    }

    [Fact]
    public async Task ZeroChatId_IsSkipped()
    {
        var transport = new FakeTransport();

        await Notifier(transport, "bot-token").SendAsync(0, "текст");

        Assert.Empty(transport.Calls);
    }

    [Fact]
    public async Task NetworkFailure_IsRetried_ButBadChatIsNot()
    {
        // Сеть дважды падает, третья попытка проходит: первая + 2 повтора.
        var flaky = new FakeTransport { Failures = new Queue<Exception>([new HttpRequestException("сеть"), new RequestException("таймаут")]) };
        await Notifier(flaky, "bot-token").SendAsync(1, "текст");
        Assert.Equal(3, flaky.Attempts);
        Assert.Single(flaky.Calls);

        // Сеть лежит всё время: после повторов сдаёмся без исключения.
        var down = new FakeTransport { Failures = new Queue<Exception>(Enumerable.Range(0, 10).Select(_ => (Exception)new HttpRequestException("сеть"))) };
        await Notifier(down, "bot-token").SendAsync(1, "текст");
        Assert.Equal(3, down.Attempts);
        Assert.Empty(down.Calls);

        // Неверный чат (400) повтор не исправит — одна попытка.
        var badChat = new FakeTransport { Failures = new Queue<Exception>([new ApiRequestException("Bad Request: chat not found", 400)]) };
        await Notifier(badChat, "bot-token").SendAsync(1, "текст");
        Assert.Equal(1, badChat.Attempts);
    }

    private static TelegramNotifier Notifier(FakeTransport transport, string? token) =>
        new(Options.Create(new TelegramOptions { BotToken = token, RetryDelay = TimeSpan.Zero }), transport, NullLogger<TelegramNotifier>.Instance);

    private sealed class FakeTransport : ITelegramTransport
    {
        public Queue<Exception> Failures { get; init; } = new();
        public List<(string Token, long ChatId, string Text)> Calls { get; } = [];
        public int Attempts { get; private set; }

        public Task SendAsync(string token, long chatId, string markdown, CancellationToken ct)
        {
            Attempts++;
            if (Failures.TryDequeue(out var failure))
                throw failure;
            Calls.Add((token, chatId, markdown));
            return Task.CompletedTask;
        }
    }
}

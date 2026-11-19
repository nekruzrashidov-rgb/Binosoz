using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Infrastructure.Services;

namespace Sitora.Tests.Infrastructure;

// Очередь сообщений покупателю в тестах: задача отправки выполняется сразу, в своей области DI (как в Hangfire).
// Сбой «будет повтор» не выходит наружу — его проверяют тесты, вызывая BuyerMessageJob ещё раз.
public class InlineBuyerMessageQueue(IServiceScopeFactory scopes, InlineBuyerMessageQueue.Log log) : IBuyerMessageQueue
{
    public class Log
    {
        public ConcurrentQueue<Guid> Enqueued { get; } = new();
        public ConcurrentQueue<Guid> Retried { get; } = new();
    }

    public void Enqueue(Guid messageId)
    {
        log.Enqueued.Enqueue(messageId);
        using var scope = scopes.CreateScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<BuyerMessageJob>().SendAsync(messageId, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (BuyerMessageRetryException)
        {
            log.Retried.Enqueue(messageId);
        }
    }
}

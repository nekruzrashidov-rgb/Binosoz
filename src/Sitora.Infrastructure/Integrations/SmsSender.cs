using Microsoft.Extensions.Logging;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Заглушка: SMS не отправляются, текст (вместе с кодом) пишется в лог.
// До запуска в продакшен её нужно заменить настоящим провайдером.
public class SmsSender(ILogger<SmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phone, string text, CancellationToken ct = default)
    {
        logger.LogInformation("[SMS, заглушка] {Phone}: {Text}", phone, text);
        return Task.CompletedTask;
    }
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Заглушка: SMS не отправляются. В Development текст вместе с кодом пишется в лог, чтобы можно было войти.
// В других окружениях код в лог не попадает (иначе его прочитал бы любой с доступом к логам),
// поэтому вход по SMS там заработает только с настоящим провайдером.
public class SmsSender(IHostEnvironment environment, ILogger<SmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phone, string text, CancellationToken ct = default)
    {
        if (environment.IsDevelopment())
            logger.LogInformation("[SMS, заглушка] {Phone}: {Text}", phone, text);
        else
            logger.LogWarning("[SMS, заглушка] SMS на {Phone} не отправлено: провайдер не подключён", Mask(phone));
        return Task.CompletedTask;
    }

    private static string Mask(string phone) => phone.Length > 4 ? new string('*', phone.Length - 4) + phone[^4..] : "****";
}

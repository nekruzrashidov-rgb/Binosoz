using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Health;

namespace Sitora.Infrastructure.Jobs;

// Последнее известное состояние, чтобы сообщать только о переходах «упало» и «восстановилось».
public class HealthAlertState
{
    public HealthStatus? Last { get; set; }
}

// Раз в 5 минут проверяет зависимости и шлёт уведомление в канал менеджеров при смене состояния.
// Полное падение процесса так не заметить — для него нужна внешняя проверка (см. docs/DEPLOY.md).
public class HealthAlertJob(
    HealthCheckService health,
    INotificationSender notifications,
    HealthAlertState state,
    ILogger<HealthAlertJob> logger)
{
    public const string Id = "health-alert";
    public const string Schedule = "*/5 * * * *";

    public async Task RunAsync(CancellationToken ct)
    {
        var report = await health.CheckHealthAsync(r => r.Tags.Contains(HealthTags.Ready), ct);
        var previous = state.Last;
        state.Last = report.Status;

        var wasHealthy = previous is null or HealthStatus.Healthy;
        if (report.Status != HealthStatus.Healthy && (wasHealthy || previous != report.Status))
        {
            var failed = report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy)
                .Select(e => $"{e.Key}: {Describe(e.Value.Status)}");
            var text = $"Sitora API: {Describe(report.Status)}. Проблемы: {string.Join(", ", failed)}";
            logger.LogError("{Alert}", text);
            await notifications.SendAsync(null, text, ct);
        }
        else if (report.Status == HealthStatus.Healthy && previous is not null and not HealthStatus.Healthy)
        {
            logger.LogInformation("Sitora API снова работает нормально");
            await notifications.SendAsync(null, "Sitora API снова работает нормально", ct);
        }
    }

    private static string Describe(HealthStatus status) => status switch
    {
        HealthStatus.Unhealthy => "не работает",
        HealthStatus.Degraded => "работает с ограничениями",
        _ => "работает"
    };
}

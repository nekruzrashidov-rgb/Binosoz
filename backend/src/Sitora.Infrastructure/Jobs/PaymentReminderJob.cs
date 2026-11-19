using Sitora.Infrastructure.Services;

namespace Sitora.Infrastructure.Jobs;

// Напоминания покупателю: за 3 дня до срока и в день срока.
public class PaymentReminderJob(PaymentAlertService alerts)
{
    public const string Id = "payment-reminders";
    // 05:00 UTC — 10:00 по Душанбе, вместе с уведомлениями о просрочке.
    public const string Schedule = "0 5 * * *";

    public async Task RunAsync(CancellationToken ct)
    {
        await alerts.RemindUpcomingAsync(ct);
        await alerts.RemindDueTodayAsync(ct);
    }
}

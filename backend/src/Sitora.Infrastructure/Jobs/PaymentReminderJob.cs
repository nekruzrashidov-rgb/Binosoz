using Sitora.Infrastructure.Services;

namespace Sitora.Infrastructure.Jobs;

public class PaymentReminderJob(PaymentAlertService alerts)
{
    public const string Id = "payment-reminders";
    // 05:00 UTC — 10:00 по Душанбе, вместе с уведомлениями о просрочке.
    public const string Schedule = "0 5 * * *";

    public Task RunAsync(CancellationToken ct) => alerts.RemindUpcomingAsync(ct);
}

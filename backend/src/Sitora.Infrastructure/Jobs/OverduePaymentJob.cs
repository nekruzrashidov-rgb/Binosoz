using Sitora.Infrastructure.Services;

namespace Sitora.Infrastructure.Jobs;

public class OverduePaymentJob(PaymentAlertService alerts)
{
    public const string Id = "overdue-payments";
    // 05:00 UTC — 10:00 по Душанбе.
    public const string Schedule = "0 5 * * *";

    public Task RunAsync(CancellationToken ct) => alerts.NotifyOverdueAsync(ct);
}

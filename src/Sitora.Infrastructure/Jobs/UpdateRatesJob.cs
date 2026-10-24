using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Jobs;

public class UpdateRatesJob(IExchangeRateService rates)
{
    public const string Id = "update-rates";
    // 05:00 UTC — 10:00 по Душанбе, после публикации курса НБТ.
    public const string Schedule = "0 5 * * *";

    public Task RunAsync(CancellationToken ct) => rates.UpdateFromProviderAsync(ct);
}

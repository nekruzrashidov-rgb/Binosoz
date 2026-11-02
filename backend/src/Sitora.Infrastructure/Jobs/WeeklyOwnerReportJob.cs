using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Jobs;

public class WeeklyOwnerReportJob(IWeeklyReportService reports)
{
    public const string Id = "weekly-owner-report";
    // Понедельник 04:00 UTC — 09:00 по Душанбе.
    public const string Schedule = "0 4 * * 1";

    public Task RunAsync(CancellationToken ct) => reports.GenerateForAllAsync(ct);
}

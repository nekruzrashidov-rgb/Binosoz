using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Jobs;

public class UpdateRatesJob(IExchangeRateService rates)
{
    public const string Id = "update-rates";
    // 04:00 и 06:00 UTC — 09:00 и 11:00 по Душанбе: второй запуск — если к девяти курс ещё не опубликован или сайт не ответил.
    // Курс на дату обновляется, а введённый вручную не перезаписывается.
    public const string Schedule = "0 4,6 * * *";

    public Task RunAsync(CancellationToken ct) => rates.UpdateFromProviderAsync(ct);
}

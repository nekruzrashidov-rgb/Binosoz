using Sitora.Domain.Interfaces;

namespace Sitora.Tests.Infrastructure;

// Вместо сайта НБТ: тесты не ходят в сеть. По умолчанию курса нет, как при недоступном сайте.
public class FakeRateProvider : IRateProvider
{
    public decimal? Usd { get; set; }

    public Task<decimal?> GetUsdRateAsync(DateOnly date, CancellationToken ct = default) => Task.FromResult(Usd);
}

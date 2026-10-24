using Microsoft.Extensions.Logging;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Заглушка: загрузка курса с сайта НБТ ещё не подключена, курс вводит бухгалтер (PUT /rates).
public class NbtRateProvider(ILogger<NbtRateProvider> logger) : IRateProvider
{
    public Task<decimal?> GetUsdRateAsync(DateOnly date, CancellationToken ct = default)
    {
        logger.LogWarning("[НБТ, заглушка] Курс USD на {Date} не загружен: введите его вручную", date);
        return Task.FromResult<decimal?>(null);
    }
}

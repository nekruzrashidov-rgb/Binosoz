using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;

namespace Sitora.Domain.Interfaces;

public interface IExchangeRateService
{
    Task<RateResponse> GetCurrentAsync(Currency currency, CancellationToken ct = default);

    // Последний курс не позже сегодняшнего дня; null, если курс ещё не вводили.
    Task<RateResponse?> FindCurrentAsync(Currency currency, CancellationToken ct = default);
    Task<RateResponse> SetAsync(SetRateRequest request, CancellationToken ct = default);

    // Курс на дату или ближайший более ранний (НБТ не публикует курс в выходные); null, если курса нет.
    Task<decimal?> GetRateOnAsync(Currency currency, DateOnly date, CancellationToken ct = default);

    Task UpdateFromProviderAsync(CancellationToken ct = default);
}

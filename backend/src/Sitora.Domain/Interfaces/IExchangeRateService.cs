using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;

namespace Sitora.Domain.Interfaces;

// organizationId — чей ручной курс учитывать вместе с общим курсом НБТ; null — только общий.
public interface IExchangeRateService
{
    Task<RateResponse> GetCurrentAsync(Currency currency, Guid? organizationId, CancellationToken ct = default);

    // Последний курс не позже сегодняшнего дня; null, если курс ещё не вводили.
    Task<RateResponse?> FindCurrentAsync(Currency currency, Guid? organizationId, CancellationToken ct = default);

    // Ручной курс: в организации бухгалтера; у создателя платформы без организации — общий.
    Task<RateResponse> SetAsync(SetRateRequest request, CancellationToken ct = default);

    // Курс на дату или ближайший более ранний (НБТ не публикует курс в выходные); null, если курса нет.
    Task<decimal?> GetRateOnAsync(Currency currency, DateOnly date, Guid? organizationId, CancellationToken ct = default);

    Task UpdateFromProviderAsync(CancellationToken ct = default);
}

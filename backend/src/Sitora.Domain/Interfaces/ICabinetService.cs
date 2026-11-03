using Sitora.Domain.DTOs.Sales;

namespace Sitora.Domain.Interfaces;

// Личный кабинет покупателя: сводка по своим сделкам (только своим — по Customer.UserId).
public interface ICabinetService
{
    Task<CabinetOverviewResponse> GetOverviewAsync(CancellationToken ct = default);
}

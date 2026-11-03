using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Platform;

// Тариф: MaxBuildings / MaxUsers null — без ограничения. OrganizationsCount — у скольких организаций он действует сейчас.
public record TariffPlanResponse(
    Guid Id,
    string Name,
    decimal Price,
    Currency Currency,
    int? MaxBuildings,
    int? MaxUsers,
    IReadOnlyList<string> Features,
    bool IsActive,
    int OrganizationsCount);

public record SaveTariffRequest(
    string Name,
    decimal Price,
    Currency Currency = Currency.TJS,
    int? MaxBuildings = null,
    int? MaxUsers = null,
    IReadOnlyList<string>? Features = null,
    bool IsActive = true);

// Назначить тариф организации с сегодняшнего дня; EndsAt null — бессрочно. Прежний тариф закрывается.
public record AssignTariffRequest(Guid TariffPlanId, DateTime? EndsAt = null, bool IsPaid = false);

public record OrganizationTariffResponse(
    Guid Id,
    Guid OrganizationId,
    Guid TariffPlanId,
    string TariffName,
    decimal Price,
    Currency Currency,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsPaid);

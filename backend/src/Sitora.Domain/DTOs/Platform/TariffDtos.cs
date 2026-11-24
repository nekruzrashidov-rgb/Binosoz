using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Platform;

// Тариф: MaxBuildings / MaxUsers null — без ограничения. OrganizationsCount — у скольких организаций он действует сейчас.
// Name / Features — русские (их правит форма), *Tg / *En — переводы; LocalName / LocalFeatures — на языке запроса
// (Accept-Language), без перевода — русские (R-03).
public record TariffPlanResponse(
    Guid Id,
    string Name,
    decimal Price,
    Currency Currency,
    int? MaxBuildings,
    int? MaxUsers,
    IReadOnlyList<string> Features,
    bool IsActive,
    int OrganizationsCount,
    string? NameTg = null,
    string? NameEn = null,
    IReadOnlyList<string>? FeaturesTg = null,
    IReadOnlyList<string>? FeaturesEn = null,
    string? LocalName = null,
    IReadOnlyList<string>? LocalFeatures = null);

public record SaveTariffRequest(
    string Name,
    decimal Price,
    Currency Currency = Currency.TJS,
    int? MaxBuildings = null,
    int? MaxUsers = null,
    IReadOnlyList<string>? Features = null,
    bool IsActive = true,
    string? NameTg = null,
    string? NameEn = null,
    IReadOnlyList<string>? FeaturesTg = null,
    IReadOnlyList<string>? FeaturesEn = null);

// Назначить тариф организации с сегодняшнего дня; EndsAt null — бессрочно. Прежний тариф закрывается.
public record AssignTariffRequest(Guid TariffPlanId, DateTime? EndsAt = null, bool IsPaid = false);

// TariffName — на языке запроса (без перевода — русское).
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

// Тариф своей организации для CRM → «Настройки» (C-07): тот же действующий тариф, что видит платформа
// (OrganizationTariffs). Name null — тариф не назначен, лимитов нет. Buildings / Users — сколько уже есть.
public record OwnTariffResponse(
    string? Name,
    IReadOnlyList<string> Features,
    int? MaxBuildings,
    int? MaxUsers,
    int Buildings,
    int Users,
    DateTime? EndsAt);

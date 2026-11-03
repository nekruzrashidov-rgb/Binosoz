using Sitora.Domain.DTOs.Auth;

namespace Sitora.Domain.DTOs.Platform;

// Владелец организации в списках суперадмина.
public record OrganizationOwnerInfo(Guid Id, string FullName, string? Email, string? Phone);

public record OrganizationBuildingBrief(Guid Id, string Name, int ApartmentsCount, int FreeApartments);

// Карточка организации: всё из списка и статистика — платежи (в сомони), квартиры, дома, история тарифов.
public record OrganizationDetailsResponse(
    OrganizationResponse Organization,
    decimal PaymentsSumTjs,
    int ApartmentsCount,
    int FreeApartments,
    int SoldApartments,
    IReadOnlyList<OrganizationBuildingBrief> Buildings,
    IReadOnlyList<OrganizationTariffResponse> TariffHistory);

// Название и короткое имя (поддомен); без slug — прежнее.
public record UpdateOrganizationRequest(string Name, string? Slug = null);

// Новый владелец организации: пароль генерируется и приходит письмом.
public record InviteOwnerRequest(string Email, string FirstName, string LastName);

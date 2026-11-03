namespace Sitora.Domain.DTOs.Auth;

// BuildingIds пустой — сотрудник видит все дома организации.
public record UserResponse(
    Guid Id,
    string? Email,
    string? Phone,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTime CreatedAt,
    Guid? OrganizationId = null,
    IReadOnlyList<Guid>? BuildingIds = null);

// Сотрудник в организации текущего руководителя. Без пароля он генерируется и уходит сотруднику письмом.
// Покупатели заводятся сами при регистрации или первом входе по почте.
public record CreateUserRequest(
    string Email,
    string FullName,
    string Role,
    string? Password,
    string? Phone,
    IReadOnlyList<Guid>? BuildingIds = null);

// Блокировка (IsActive = false) закрывает вход и обновление сессий; выданный access-токен живёт до 15 минут.
public record SetUserStatusRequest(bool IsActive);

// Дома сотрудника: пусто — все дома организации; у админа объекта ровно один.
public record SetUserBuildingsRequest(IReadOnlyList<Guid> BuildingIds);

// Приглашение сотрудника: учётная запись создаётся сразу, пароль (8 символов) приходит письмом.
// BuildingId — один дом (обязателен админу объекта); BuildingIds — несколько; ничего — все дома организации.
public record CreateInvitationRequest(
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid? BuildingId = null,
    IReadOnlyList<Guid>? BuildingIds = null);

public record InvitationResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    IReadOnlyList<Guid> BuildingIds,
    DateTime ExpiresAt,
    DateTime? AcceptedAt,
    DateTime CreatedAt);

// Приглашения по ссылке из старых писем: пароль обязателен — сотрудники входят только по паролю.
public record AcceptInvitationRequest(string Token, string? Password = null);

// Что показать на странице приглашения до того, как его примут.
public record InvitationInfoResponse(string OrganizationName, string FullName, string Email, string Role, DateTime ExpiresAt);

// Организации — только для создателя платформы.
// Ещё один владелец организации (создатель платформы). Без пароля он генерируется и уходит владельцу письмом.
public record AddOwnerRequest(string Email, string FullName, string? Password);

public record CreateOrganizationRequest(string Name, string Slug, string OwnerEmail, string OwnerFullName);

public record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    int BuildingsCount,
    int UsersCount,
    DateTime CreatedAt,
    string? CustomDomain = null,
    string? LogoUrl = null,
    IReadOnlyList<string>? OwnerEmails = null,
    IReadOnlyList<string>? BuildingNames = null,
    // Для раздела суперадмина: сделки и покупатели, последнее действие по журналу, действующий тариф, владельцы.
    int DealsCount = 0,
    int CustomersCount = 0,
    DateTime? LastActivityAt = null,
    Sitora.Domain.DTOs.Platform.OrganizationTariffResponse? Tariff = null,
    IReadOnlyList<Sitora.Domain.DTOs.Platform.OrganizationOwnerInfo>? Owners = null);

// Приостановить (false) или снова открыть (true) организацию: при приостановке её сотрудники выходят из системы.
public record OrganizationStatusRequest(bool IsActive);

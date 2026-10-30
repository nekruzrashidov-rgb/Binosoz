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

// Сотрудник в организации текущего руководителя. Пароль необязателен: вход по коду из почты.
// Покупатели заводятся сами при первом входе по почте.
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

public record CreateInvitationRequest(string Email, string FullName, string Role, IReadOnlyList<Guid>? BuildingIds);

public record InvitationResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    IReadOnlyList<Guid> BuildingIds,
    DateTime ExpiresAt,
    DateTime? AcceptedAt,
    DateTime CreatedAt);

// Пароль необязателен: без него сотрудник входит только по коду из почты, с ним — ещё и через /auth/login.
public record AcceptInvitationRequest(string Token, string? Password = null);

// Что показать на странице приглашения до того, как его примут.
public record InvitationInfoResponse(string OrganizationName, string FullName, string Email, string Role, DateTime ExpiresAt);

// Организации — только для создателя платформы.
// Ещё один владелец организации (создатель платформы). Пароль необязателен: владелец входит по коду из почты.
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
    IReadOnlyList<string>? BuildingNames = null);

// Приостановить (false) или снова открыть (true) организацию: при приостановке её сотрудники выходят из системы.
public record OrganizationStatusRequest(bool IsActive);

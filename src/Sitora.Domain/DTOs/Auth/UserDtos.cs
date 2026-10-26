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

public record AcceptInvitationRequest(string Token);

// Организации — только для создателя платформы.
public record CreateOrganizationRequest(string Name, string Slug, string OwnerEmail, string OwnerFullName);

public record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    int BuildingsCount,
    int UsersCount,
    DateTime CreatedAt,
    string? CustomDomain = null);

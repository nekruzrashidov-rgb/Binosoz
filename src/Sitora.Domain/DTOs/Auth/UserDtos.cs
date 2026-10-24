namespace Sitora.Domain.DTOs.Auth;

public record UserResponse(
    Guid Id,
    string? Email,
    string? Phone,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTime CreatedAt);

// Создание сотрудника администратором; покупатели заводятся сами при первом входе по SMS.
public record CreateUserRequest(string Email, string FullName, string Role, string Password, string? Phone);

// Блокировка (IsActive = false) закрывает вход и обновление сессий; выданный access-токен живёт до 15 минут.
public record SetUserStatusRequest(bool IsActive);

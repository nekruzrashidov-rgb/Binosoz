namespace Sitora.Domain.Entities;

// Одноразовый код входа по почте. Хранится только хеш кода.
// Запись создаётся на каждый запрос кода, даже для незнакомой почты (Sent = false):
// так лимиты и ответ одинаковы для всех и по ним нельзя узнать, есть ли учётная запись.
public class EmailCode : BaseEntity
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string? RequestIp { get; set; }
    public bool Sent { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsUsable(DateTime now) => Sent && UsedAt is null && ExpiresAt > now && Attempts < MaxAttempts;
}

// Приглашение сотрудника: Owner или ObjectAdmin зовёт человека по почте на должность в своих домах.
public class Invitation : BaseEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<Guid> BuildingIds { get; set; } = [];

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid InvitedById { get; set; }

    public bool IsPending(DateTime now) => AcceptedAt is null && RevokedAt is null && ExpiresAt > now;
}

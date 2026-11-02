namespace Sitora.Domain.Entities;

// Ссылка «Забыли пароль?» для сотрудника: живёт 24 часа, срабатывает один раз.
// Храним только SHA-256 хеш токена, как у refresh-токенов: утечка базы не даёт рабочих ссылок.
public class PasswordResetToken : BaseEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsUsable(DateTime now) => UsedAt is null && ExpiresAt > now;
}

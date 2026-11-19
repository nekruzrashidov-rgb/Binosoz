namespace Sitora.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public const int UserAgentMaxLength = 300;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    // Храним только SHA-256 хеш: утечка базы не даёт рабочих токенов.
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    // Вход на одном устройстве: при ротации новый токен наследует сессию, дату входа и браузер.
    // По ним кабинет показывает «Устройства» и закрывает один вход или все.
    public Guid SessionId { get; set; }
    public DateTime SessionStartedAt { get; set; }
    public string? UserAgent { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}

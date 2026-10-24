namespace Sitora.Domain.Entities;

public class SmsCode : BaseEntity
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public string Phone { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsUsable(DateTime now) => UsedAt is null && ExpiresAt > now && Attempts < MaxAttempts;
}

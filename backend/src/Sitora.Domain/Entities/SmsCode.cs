namespace Sitora.Domain.Entities;

public class SmsCode : BaseEntity
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public const string ChannelSms = "sms";
    public const string ChannelTelegram = "telegram";
    // Код в Telegram (Telegram Gateway) живёт 10 минут, как код из письма.
    public static readonly TimeSpan TelegramLifetime = TimeSpan.FromMinutes(10);

    public string Phone { get; set; } = string.Empty;
    // Чем отправлен код: sms или telegram (Telegram Gateway); для Telegram — номер запроса и отчёт о доставке.
    public string Channel { get; set; } = ChannelSms;
    public string? RequestIp { get; set; }
    public string? GatewayRequestId { get; set; }
    public string? DeliveryStatus { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsUsable(DateTime now) => UsedAt is null && ExpiresAt > now && Attempts < MaxAttempts;
}

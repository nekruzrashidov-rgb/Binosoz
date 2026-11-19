using Microsoft.AspNetCore.Identity;

namespace Sitora.Domain.Entities;

public class AppUser : IdentityUser<Guid>, IHasTimestamps
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public List<UserBuilding> Buildings { get; set; } = [];

    // Личный чат в Telegram для уведомлений; null — не привязан.
    public long? TelegramChatId { get; set; }

    // Уведомления покупателю: каналы (Telegram и почта — оба, что подключено, по умолчанию), какие события присылать
    // и язык сообщений (ru, tg, en — как выбран на сайте при сохранении настроек или привязке Telegram).
    public bool NotifyByTelegram { get; set; } = true;
    public bool NotifyByEmail { get; set; } = true;
    public NotifyEventGroups NotifyEvents { get; set; } = NotifyEventGroups.All;
    public string? NotifyLanguage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

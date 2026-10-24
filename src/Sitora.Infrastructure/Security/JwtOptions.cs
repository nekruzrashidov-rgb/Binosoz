using System.ComponentModel.DataAnnotations;

namespace Sitora.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    // Задаётся только через переменную окружения Jwt__Key.
    [Required(ErrorMessage = "Не задан ключ подписи JWT (переменная окружения Jwt__Key)")]
    [MinLength(32, ErrorMessage = "Ключ подписи JWT должен быть не короче 32 символов")]
    public string Key { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}

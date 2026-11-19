namespace Sitora.Infrastructure.Security;

// Понятное имя устройства из заголовка User-Agent для списка «Устройства»: «Chrome · Android», «Safari · iPhone».
// Сам заголовок хранится обрезанным, без IP-адреса.
public static class DeviceNames
{
    public static string? Trim(string? userAgent)
    {
        var ua = userAgent?.Trim();
        if (string.IsNullOrEmpty(ua))
            return null;
        return ua.Length > Domain.Entities.RefreshToken.UserAgentMaxLength ? ua[..Domain.Entities.RefreshToken.UserAgentMaxLength] : ua;
    }

    // null — устройство не распознано (сайт пишет «Неизвестное устройство» на языке покупателя).
    public static string? Describe(string? userAgent)
    {
        var ua = userAgent ?? "";
        var browser = Has(ua, "Edg/") ? "Edge"
            : Has(ua, "OPR/") || Has(ua, "Opera") ? "Opera"
            : Has(ua, "YaBrowser") ? "Яндекс Браузер"
            : Has(ua, "Firefox/") || Has(ua, "FxiOS") ? "Firefox"
            : Has(ua, "Chrome/") || Has(ua, "CriOS") ? "Chrome"
            : Has(ua, "Safari/") ? "Safari"
            : null;
        var system = Has(ua, "iPhone") ? "iPhone"
            : Has(ua, "iPad") ? "iPad"
            : Has(ua, "Android") ? "Android"
            : Has(ua, "Windows") ? "Windows"
            : Has(ua, "Mac OS X") || Has(ua, "Macintosh") ? "macOS"
            : Has(ua, "Linux") ? "Linux"
            : null;
        return (browser, system) switch
        {
            (null, null) => null,
            (null, _) => system!,
            (_, null) => browser,
            _ => $"{browser} · {system}"
        };
    }

    private static bool Has(string ua, string part) => ua.Contains(part, StringComparison.OrdinalIgnoreCase);
}

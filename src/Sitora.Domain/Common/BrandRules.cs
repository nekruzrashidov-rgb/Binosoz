using System.Text.RegularExpressions;

namespace Sitora.Domain.Common;

// Правила бренда застройщика: короткое имя (оно же поддомен), свой домен и цвет.
public static partial class BrandRules
{
    public const string SlugMessage = "Короткое имя — латинские буквы, цифры и дефис, от 2 до 64 символов, без дефиса в начале и в конце";

    // Поддомены, которые нужны самой платформе.
    public static readonly IReadOnlySet<string> ReservedSlugs = new HashSet<string>(StringComparer.Ordinal)
    {
        "admin", "www", "api", "app", "mail", "smtp", "cdn", "static", "platform", "crm", "auth", "login"
    };

    // null — имя некорректное. Регистр и пробелы по краям не важны.
    public static string? NormalizeSlug(string? slug)
    {
        var value = slug?.Trim().ToLowerInvariant();
        return value is not null && SlugPattern().IsMatch(value) ? value : null;
    }

    public static bool IsReserved(string slug) => ReservedSlugs.Contains(slug);

    // Домен без схемы, пути и порта, в нижнем регистре; null — некорректный.
    public static string? NormalizeDomain(string? domain)
    {
        var value = domain?.Trim().TrimEnd('.').ToLowerInvariant();
        return value is { Length: <= 253 } && DomainPattern().IsMatch(value) ? value : null;
    }

    // Хост из запроса сайта: без порта и точки в конце.
    public static string? NormalizeHost(string? host)
    {
        var value = host?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value))
            return null;
        var colon = value.LastIndexOf(':');
        if (colon > 0 && !value.Contains(']'))
            value = value[..colon];
        return value.TrimEnd('.');
    }

    public static bool IsColor(string value) => ColorPattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex(@"^(?=.{4,253}$)([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$")]
    private static partial Regex DomainPattern();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();
}

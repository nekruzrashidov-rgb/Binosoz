namespace Sitora.Domain.Common;

public static class PhoneNumber
{
    private const string CountryCode = "992";

    // Приводит номер Таджикистана к виду +992XXXXXXXXX; null, если номер неверный.
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 9)
            digits = CountryCode + digits;

        return digits.Length == 12 && digits.StartsWith(CountryCode) ? "+" + digits : null;
    }
}

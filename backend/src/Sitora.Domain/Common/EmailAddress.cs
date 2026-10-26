using System.Net.Mail;

namespace Sitora.Domain.Common;

public static class EmailAddress
{
    // Почта для поиска и хранения: без пробелов по краям, в нижнем регистре; null — не похоже на адрес.
    public static string? Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        var value = email.Trim().ToLowerInvariant();
        return value.Length <= 256 && MailAddress.TryCreate(value, out var parsed) && parsed.Address == value ? value : null;
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sitora.Domain.DTOs.Common;

namespace Sitora.Infrastructure.Security;

public enum FileLinkKind
{
    Document = 'd',
    Photo = 'p'
}

// Подписанные ссылки на файлы: «тип.id.срок» + HMAC-SHA256. Ключ выводится из ключа JWT,
// поэтому отдельный секрет не нужен, а подпись JWT и ссылок всё равно разными ключами.
public class FileLinkSigner(IOptions<JwtOptions> jwt, TimeProvider timeProvider)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    // Токен в строке запроса, а не в пути: Serilog пишет в лог только путь.
    public const string UrlPrefix = "/api/v1/files?token=";

    private readonly byte[] _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwt.Value.Key), "sitora-file-links-v1"u8);

    public FileLinkResponse CreateLink(FileLinkKind kind, Guid id)
    {
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.Add(Lifetime);
        return new FileLinkResponse(UrlPrefix + CreateToken(kind, id, expiresAt), expiresAt);
    }

    public string CreateToken(FileLinkKind kind, Guid id, DateTime expiresAtUtc)
    {
        var unix = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var payload = Encoding.ASCII.GetBytes($"{(char)kind}.{id:N}.{unix}");
        return Base64UrlEncoder.Encode(payload) + "." + Base64UrlEncoder.Encode(HMACSHA256.HashData(_key, payload));
    }

    public bool TryRead(string? token, out FileLinkKind kind, out Guid id)
    {
        kind = default;
        id = Guid.Empty;
        var parts = token?.Split('.');
        if (parts is not { Length: 2 })
            return false;

        byte[] payload, signature;
        try
        {
            payload = Base64UrlEncoder.DecodeBytes(parts[0]);
            signature = Base64UrlEncoder.DecodeBytes(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(_key, payload)))
            return false;

        var fields = Encoding.ASCII.GetString(payload).Split('.');
        if (fields.Length != 3 || fields[0].Length != 1
            || !Guid.TryParseExact(fields[1], "N", out id)
            || !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var unix))
            return false;

        kind = (FileLinkKind)fields[0][0];
        return Enum.IsDefined(kind) && DateTimeOffset.FromUnixTimeSeconds(unix) > timeProvider.GetUtcNow();
    }
}

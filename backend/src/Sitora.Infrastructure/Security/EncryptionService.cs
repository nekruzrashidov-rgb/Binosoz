using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Sitora.Infrastructure.Security;

public class EncryptionOptions : IValidatableObject
{
    public const string SectionName = "Encryption";

    // 32 байта в base64; задаётся только переменной окружения Encryption__Key.
    public string Key { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        byte[]? bytes = null;
        try
        {
            bytes = Convert.FromBase64String(Key);
        }
        catch (FormatException)
        {
        }

        if (bytes is not { Length: 32 })
            yield return new ValidationResult("Ключ шифрования (Encryption__Key) должен быть 32 байтами в base64");
    }
}

// AES-256-GCM. Формат строки: "v1:" + base64(nonce | шифротекст | тег).
public class EncryptionService(IOptions<EncryptionOptions> options)
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key = Convert.FromBase64String(options.Value.Key);

    public string? Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return null;

        var plain = Encoding.UTF8.GetBytes(plainText);
        var buffer = new byte[NonceSize + plain.Length + TagSize];
        var nonce = buffer.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, buffer.AsSpan(NonceSize, plain.Length), buffer.AsSpan(NonceSize + plain.Length));
        return Prefix + Convert.ToBase64String(buffer);
    }

    public string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return null;
        if (!cipherText.StartsWith(Prefix))
            throw new CryptographicException("Неизвестный формат зашифрованных данных");

        var buffer = Convert.FromBase64String(cipherText[Prefix.Length..]);
        var plainLength = buffer.Length - NonceSize - TagSize;
        var plain = new byte[plainLength];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(buffer.AsSpan(0, NonceSize), buffer.AsSpan(NonceSize, plainLength), buffer.AsSpan(NonceSize + plainLength), plain);
        return Encoding.UTF8.GetString(plain);
    }
}

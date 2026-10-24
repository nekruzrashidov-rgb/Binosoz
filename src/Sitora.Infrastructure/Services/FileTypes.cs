using Sitora.Domain.Exceptions;

namespace Sitora.Infrastructure.Services;

// Разрешённые форматы файлов и их первые байты: одному расширению верить нельзя.
public static class FileTypes
{
    private static readonly (string ContentType, byte[] Signature) Pdf = ("application/pdf", "%PDF"u8.ToArray());
    private static readonly (string ContentType, byte[] Signature) Png = ("image/png", [0x89, 0x50, 0x4E, 0x47]);
    private static readonly (string ContentType, byte[] Signature) Jpeg = ("image/jpeg", [0xFF, 0xD8, 0xFF]);
    private static readonly (string ContentType, byte[] Signature) Docx =
        ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B, 0x03, 0x04]);

    public static readonly IReadOnlyDictionary<string, (string ContentType, byte[] Signature)> Documents =
        new Dictionary<string, (string, byte[])>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = Pdf, [".png"] = Png, [".jpg"] = Jpeg, [".jpeg"] = Jpeg, [".docx"] = Docx
        };

    public static readonly IReadOnlyDictionary<string, (string ContentType, byte[] Signature)> Images =
        new Dictionary<string, (string, byte[])>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = Png, [".jpg"] = Jpeg, [".jpeg"] = Jpeg
        };

    // Читает файл в память, проверяет размер, расширение и подпись; возвращает тип содержимого.
    public static async Task<(MemoryStream Buffer, string ContentType, string Extension)> ReadCheckedAsync(
        IReadOnlyDictionary<string, (string ContentType, byte[] Signature)> allowed,
        string fileName, long length, Stream content, long maxSize, string allowedNames, CancellationToken ct)
    {
        if (length <= 0 || length > maxSize)
            throw new BusinessRuleException($"Размер файла должен быть от 1 байта до {maxSize / 1024 / 1024} МБ");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!allowed.TryGetValue(extension, out var type))
            throw new BusinessRuleException($"Допустимые форматы: {allowedNames}");

        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > maxSize || !buffer.GetBuffer().AsSpan(0, (int)buffer.Length).StartsWith(type.Signature))
        {
            await buffer.DisposeAsync();
            throw new BusinessRuleException("Содержимое файла не соответствует его расширению");
        }

        buffer.Position = 0;
        return (buffer, type.ContentType, extension);
    }
}

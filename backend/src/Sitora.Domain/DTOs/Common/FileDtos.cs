namespace Sitora.Domain.DTOs.Common;

// Подписанная ссылка на файл: по ней файл скачивается без входа до ExpiresAt.
// Url относительный — клиент добавляет к нему адрес API.
public record FileLinkResponse(string Url, DateTime ExpiresAt);

// Файл для отдачи клиенту. Inline — показать в браузере (фото), иначе скачать под FileName.
public record StoredFile(string? FileName, string ContentType, Stream Content, bool Inline);

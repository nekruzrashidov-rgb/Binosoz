namespace Sitora.WebApi.Extensions;

// Обязательные настройки, которые задаются только переменными окружения (секреты в репозиторий не кладём).
// Проверяем их сразу после сборки приложения, чтобы назвать все недостающие одной строкой,
// а не падать со стеком на первой из них.
public static class RequiredSettings
{
    private static readonly (string Key, string Variable) Database = ("ConnectionStrings:Default", "ConnectionStrings__Default");
    private static readonly (string Key, string Variable) JwtKey = ("Jwt:Key", "Jwt__Key");
    private static readonly (string Key, string Variable) EncryptionKey = ("Encryption:Key", "Encryption__Key");

    public static IReadOnlyList<string> Missing(IConfiguration configuration, bool migrateOnly, bool development)
    {
        // Команде миграций нужна только база; в Development она ещё и шифрует паспорта клиентов сидера.
        (string Key, string Variable)[] required = migrateOnly
            ? development ? [Database, EncryptionKey] : [Database]
            : [Database, JwtKey, EncryptionKey];

        return required
            .Where(s => string.IsNullOrWhiteSpace(configuration[s.Key]))
            .Select(s => s.Variable)
            .ToList();
    }

    public static string Message(IReadOnlyList<string> missing) =>
        $"Не заданы переменные окружения: {string.Join(", ", missing)}. " +
        "Образец значений — .env.example, описание — README, раздел «Переменные окружения».";
}

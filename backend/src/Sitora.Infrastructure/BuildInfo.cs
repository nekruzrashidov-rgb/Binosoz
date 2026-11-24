using System.Reflection;

namespace Sitora.WebApi.Extensions;

// Какая сборка работает: короткий SHA коммита и время сборки — для /health/live, чтобы отставание прода от main
// было видно без панели хостинга.
// Источники SHA по порядку: GIT_SHA (Docker-образ из CI: --build-arg GIT_SHA), RENDER_GIT_COMMIT (Render при сборке
// из репозитория), иначе «dev». Время: BUILT_AT (Docker-образ), иначе время записи сборки приложения.
public static class BuildInfo
{
    public static string Version { get; } = ShortSha(
        Environment.GetEnvironmentVariable("GIT_SHA") is { Length: > 0 } sha ? sha : Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT"));

    public static string BuiltAt { get; } = ResolveBuiltAt();

    public static string ShortSha(string? sha) =>
        string.IsNullOrWhiteSpace(sha) ? "dev" : sha.Trim()[..Math.Min(7, sha.Trim().Length)].ToLowerInvariant();

    private static string ResolveBuiltAt()
    {
        if (DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("BUILT_AT"), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var at))
            return at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var path = Assembly.GetEntryAssembly()?.Location;
        return string.IsNullOrEmpty(path) || !File.Exists(path)
            ? ""
            : File.GetLastWriteTimeUtc(path).ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}

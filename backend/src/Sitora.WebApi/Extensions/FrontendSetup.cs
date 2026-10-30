namespace Sitora.WebApi.Extensions;

// Сайт (папка frontend/) отдаёт сам API: страницы, скрипты и стили с того же адреса, что и /api/v1.
public static class FrontendSetup
{
    private const string Html = "text/html; charset=utf-8";
    private static readonly string[] Pages = ["apartments", "contact", "register", "accept-invite", "login", "cabinet", "crm", "platform"];

    // В образе Docker сайт лежит в wwwroot рядом с приложением; при dotnet run и в тестах — папка frontend/ репозитория.
    // null — сайта нет, API работает без него.
    public static string? FindWebRoot()
    {
        var published = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (File.Exists(Path.Combine(published, "index.html")))
            return published;

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var frontend = Path.Combine(dir.FullName, "frontend");
            if (File.Exists(Path.Combine(frontend, "index.html")))
                return frontend;
        }
        return null;
    }

    public static bool HasFrontend(this WebApplication app) =>
        app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists;

    // Страницы без .html в адресе: /apartments, /contact, /register, /login, /cabinet, /crm, /platform. Скрипты и стили отдаёт UseStaticFiles.
    public static void MapSitoraFrontend(this WebApplication app)
    {
        app.MapGet("/", () => Results.File("index.html", Html)).ExcludeFromDescription();
        foreach (var page in Pages)
            app.MapGet("/" + page, () => Results.File(page + ".html", Html)).ExcludeFromDescription();

        // Неизвестный адрес сайта — страница «не найдено» с кодом 404. Неизвестные адреса API отвечают как раньше (Problem Details).
        app.MapFallback(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            if (IsApiPath(context.Request.Path) || !HttpMethods.IsGet(context.Request.Method))
                return Task.CompletedTask;
            context.Response.ContentType = Html;
            return context.Response.SendFileAsync(app.Environment.WebRootFileProvider.GetFileInfo("404.html"));
        });
    }

    // Адреса API, а не сайта: им остаётся строгая политика безопасности без скриптов.
    public static bool IsApiPath(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs") || path.StartsWithSegments("/health");
}

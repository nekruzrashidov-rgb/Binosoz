using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Middleware;

// Базовые защитные заголовки для ответов API и страниц сайта.
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    // Сайт: свои скрипты и стили, библиотеки с cdnjs (Three.js, SignalR), шрифты Google, запросы только к своему API.
    // Встроенных скриптов на страницах нет; встроенные стили нужны SVG-иконкам.
    private const string SitePolicy =
        "default-src 'self'; script-src 'self' https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: blob: https:; connect-src 'self'; object-src 'none'; base-uri 'none'; " +
        "form-action 'self'; frame-ancestors 'none'";

    // Отдельная 3D-модель комплекса (nekruz3d.html на Babylon.js): её код встроен в страницу (inline-скрипт и onclick),
    // поэтому 'unsafe-inline' нужен. Чтобы это не открыло доступ к сайту, страница работает в песочнице без allow-same-origin:
    // у неё «пустой» origin — нет доступа к localStorage с токенами, cookie и API сайта. Встраивать её можно только
    // страницам самого сайта (главная и каталог). Запросов к сети у модели нет.
    public const string Model3dPath = "/nekruz3d.html";
    private const string Model3dPolicy =
        "sandbox allow-scripts allow-popups allow-popups-to-escape-sandbox allow-modals allow-downloads; " +
        "default-src 'none'; script-src 'unsafe-inline' https://cdnjs.cloudflare.com; " +
        "style-src 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; " +
        "img-src data: blob:; worker-src blob:; connect-src 'none'; object-src 'none'; base-uri 'none'; " +
        "form-action 'none'; frame-ancestors 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";

        // Swagger UI — HTML-страница со скриптами, строгая политика её сломает.
        var path = context.Request.Path;
        if (path.Equals(Model3dPath, StringComparison.OrdinalIgnoreCase))
        {
            headers.XFrameOptions = "SAMEORIGIN";
            headers.ContentSecurityPolicy = Model3dPolicy;
            return next(context);
        }

        headers.XFrameOptions = "DENY";
        if (FrontendSetup.IsApiPath(path))
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        else if (!path.StartsWithSegments("/swagger"))
            headers.ContentSecurityPolicy = SitePolicy;

        return next(context);
    }
}

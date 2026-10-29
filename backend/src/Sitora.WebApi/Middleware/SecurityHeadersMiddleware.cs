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

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";

        // Swagger UI — HTML-страница со скриптами, строгая политика её сломает.
        var path = context.Request.Path;
        if (FrontendSetup.IsApiPath(path))
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        else if (!path.StartsWithSegments("/swagger"))
            headers.ContentSecurityPolicy = SitePolicy;

        return next(context);
    }
}

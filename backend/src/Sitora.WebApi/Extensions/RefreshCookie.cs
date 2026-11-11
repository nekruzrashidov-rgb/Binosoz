using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Auth;

namespace Sitora.WebApi.Extensions;

// Refresh-токен в cookie: HttpOnly (недоступен скриптам страницы — XSS его не украдёт), SameSite=Strict, Path=/api/v1/auth.
// Режим Auth:RefreshCookie:Mode — Dual (по умолчанию): cookie и токен в теле ответа, как раньше, пока сайт не перешёл;
// Cookie — только cookie (токен в теле пустой); Body — только тело, без cookie.
// Полный переход на Cookie возможен, когда сайт и API на одном домене: сейчас сайт на Vercel, API на Render, и браузер
// не отправит SameSite-cookie API с чужого сайта (docs/SECURITY.md).
public class RefreshCookieOptions
{
    public const string SectionName = "Auth:RefreshCookie";
    public const string Name = "sitora_rt";
    public const string Path = "/api/v1/auth";

    public string Mode { get; set; } = "Dual";

    public bool WritesCookie => !string.Equals(Mode, "Body", StringComparison.OrdinalIgnoreCase);
    public bool KeepsBody => !string.Equals(Mode, "Cookie", StringComparison.OrdinalIgnoreCase);
}

// На ответах входа и обновления (AuthResponse) ставит cookie с refresh-токеном.
public class RefreshCookieFilter(IOptions<RefreshCookieOptions> options, IHostEnvironment environment) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: AuthResponse auth } result || string.IsNullOrEmpty(auth.RefreshToken))
            return;
        var o = options.Value;
        if (o.WritesCookie)
            context.HttpContext.Response.Cookies.Append(RefreshCookieOptions.Name, auth.RefreshToken, Cookie(context.HttpContext, environment, auth.RefreshTokenExpiresAt));
        if (!o.KeepsBody)
            result.Value = auth with { RefreshToken = string.Empty };
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    public static CookieOptions Cookie(HttpContext http, IHostEnvironment environment, DateTime? expires) => new()
    {
        HttpOnly = true,
        // Вне разработки — только HTTPS; локально API бывает по HTTP.
        Secure = http.Request.IsHttps || !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookieOptions.Path,
        Expires = expires is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc)),
        IsEssential = true
    };

    // Токен из тела запроса, иначе из cookie.
    public static string? Token(HttpRequest request, string? fromBody) =>
        !string.IsNullOrWhiteSpace(fromBody) ? fromBody : request.Cookies[RefreshCookieOptions.Name];
}

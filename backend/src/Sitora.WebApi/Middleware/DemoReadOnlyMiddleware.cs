using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.Exceptions;
using Sitora.Infrastructure.Data.Seed;

namespace Sitora.WebApi.Middleware;

// Демо-менеджер для жюри (demo-manager@binosoz.tj): только просмотр и чат. Любое изменяющее действие, кроме сообщения
// в чате сделки, входа/выхода и SignalR-чата, — 403 «Демо-доступ: только просмотр и чат». Удалять, сторнировать и
// менять данные демо-менеджер не может, что бы ни разрешала роль менеджера.
public class DemoReadOnlyMiddleware(RequestDelegate next)
{
    public const string Message = "Демо-доступ: только просмотр и чат";

    public Task InvokeAsync(HttpContext context)
    {
        var email = context.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                    ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (email is null || !string.Equals(email, DemoAccountsSeeder.ManagerEmail, StringComparison.OrdinalIgnoreCase))
            return next(context);

        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || Allowed(context.Request.Path, method))
            return next(context);
        throw new ForbiddenException(Message);
    }

    // Разрешено: сообщение в чат сделки (POST /api/v1/deals/{id}/messages), отметка «прочитано», вход, выход, хаб чата.
    private static bool Allowed(PathString path, string method)
    {
        var p = path.Value ?? "";
        if (p.StartsWith("/api/v1/auth/", StringComparison.OrdinalIgnoreCase) || p.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
            return true;
        var parts = p.Trim('/').Split('/');
        return HttpMethods.IsPost(method) && parts is ["api", "v1", "deals", _, "messages"]
               || parts.Length >= 5 && parts[0] == "api" && parts[2] == "deals" && parts[4] == "messages" && parts[^1] == "read";
    }
}

using Sitora.Domain.Common;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.WebApi.Middleware;

// Режим «войти как владелец»: в каждом ответе заголовок X-Impersonated-By, а каждое изменяющее действие
// (не GET) пишется в аудит с настоящим автором — даже если сервис сам аудит не ведёт или запрос не удался.
public class ImpersonationAuditMiddleware(RequestDelegate next)
{
    public const string Header = "X-Impersonated-By";

    public async Task InvokeAsync(HttpContext context)
    {
        var impersonator = context.User.FindFirst(AccessClaims.ImpersonatedBy)?.Value;
        if (impersonator is null)
        {
            await next(context);
            return;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = impersonator;
            return Task.CompletedTask;
        });

        var writes = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
                     && !HttpMethods.IsOptions(context.Request.Method);
        var status = StatusCodes.Status500InternalServerError;
        try
        {
            await next(context);
            status = context.Response.StatusCode;
        }
        finally
        {
            if (writes)
                await RecordAsync(context, status);
        }
    }

    // Отдельная область DI: контекст запроса мог остаться с несохранёнными изменениями упавшей операции,
    // и их нельзя случайно сохранить вместе с записью аудита.
    private static async Task RecordAsync(HttpContext context, int status)
    {
        await using var scope = context.RequestServices.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var organizationId = Guid.TryParse(context.User.FindFirst(AccessClaims.Organization)?.Value, out var id) ? id : Guid.Empty;
        audit.Record("Impersonation", organizationId, $"{context.Request.Method} {context.Request.Path}", null, new { Status = status });
        await db.SaveChangesAsync(context.RequestAborted.IsCancellationRequested ? CancellationToken.None : context.RequestAborted);
    }
}

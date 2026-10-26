using Microsoft.AspNetCore.Diagnostics;
using Sitora.Domain.Exceptions;

namespace Sitora.WebApi.Middleware;

// Единая точка, где исключения домена превращаются в ответы Problem Details.
public class DomainExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = Describe(exception);

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Необработанная ошибка при запросе {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Detail = exception is DomainException ? exception.Message : null
            }
        });
    }

    // Код ответа для исключения; им же пользуется аудит режима «войти как владелец».
    public static (int Status, string Title) Describe(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Не найдено"),
        ConflictException => (StatusCodes.Status409Conflict, "Конфликт"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Доступ запрещён"),
        BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Нарушено бизнес-правило"),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Требуется вход"),
        TooManyRequestsException => (StatusCodes.Status429TooManyRequests, "Слишком много запросов"),
        _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера")
    };
}

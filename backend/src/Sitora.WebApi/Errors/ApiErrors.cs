using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sitora.Domain.Exceptions;
using Sitora.WebApi.Validators;

namespace Sitora.WebApi.Errors;

// Единый вид ошибок API (Problem Details, RFC 9457): русский title и detail (таджикский при Accept-Language: tg),
// машиночитаемый code и traceId — тот же, что в записи лога о запросе. Без стектрейсов и внутренностей парсера.
public static partial class ApiErrors
{
    public const string CodeKey = "code";
    public const string TraceIdKey = "traceId";
    public const string ValidationTitle = "Проверьте введённые данные";
    public const string MalformedRequest = "Некорректный формат запроса";
    private const string InternalDetail = "Произошла ошибка. Мы уже разбираемся; если повторится, сообщите номер ошибки (traceId)";

    // Язык ответа: культура, выбранная UseRequestLocalization, а если до неё не дошло (ошибка раньше) — заголовок.
    public static bool IsTajik(HttpContext context)
    {
        var feature = context.Features.Get<IRequestCultureFeature>();
        if (feature is not null)
            return feature.RequestCulture.UICulture.TwoLetterISOLanguageName == "tg";
        var header = context.Request.Headers.AcceptLanguage.ToString();
        var first = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        return first.StartsWith("tg", StringComparison.OrdinalIgnoreCase);
    }

    public static (string Title, string Code) ForStatus(int status) => status switch
    {
        400 => ("Неверный запрос", "bad_request"),
        401 => ("Требуется вход", "unauthorized"),
        402 => ("Нужен тариф выше", "tariff_limit"),
        403 => ("Недостаточно прав", "forbidden"),
        404 => ("Не найдено", "not_found"),
        405 => ("Метод не поддерживается", "method_not_allowed"),
        409 => ("Конфликт", "conflict"),
        413 => ("Слишком большой запрос", "payload_too_large"),
        415 => ("Неподдерживаемый тип содержимого", "unsupported_media_type"),
        422 => ("Нарушено бизнес-правило", "business_rule"),
        429 => ("Слишком много запросов", "rate_limited"),
        501 => ("Не реализовано", "not_implemented"),
        503 => ("Сервис недоступен", "service_unavailable"),
        >= 500 => ("Внутренняя ошибка сервера", "internal_error"),
        _ => ("Ошибка", "error")
    };

    // Код ошибки домена: из каталога по тексту, иначе по типу исключения.
    public static string CodeFor(DomainException exception) => ErrorCatalog.TryFind(exception.Message, out var entry, out _)
        ? entry.Code
        : exception switch
        {
            NotFoundException => "not_found",
            ConflictException => "conflict",
            ForbiddenException => "forbidden",
            BusinessRuleException => "business_rule",
            BadRequestException => "bad_request",
            PaymentRequiredException => "tariff_limit",
            UnauthorizedException => "unauthorized",
            TooManyRequestsException => "rate_limited",
            ServiceUnavailableException => "service_unavailable",
            _ => "error"
        };

    // AddProblemDetails(o => o.CustomizeProblemDetails = ApiErrors.Customize): общий шаг для всех ответов с ошибкой —
    // исключений домена, кодов без тела (401, 403, 404, 405, 415 от UseStatusCodePages), лимитов и валидации.
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var http = context.HttpContext;
        var status = problem.Status ?? http.Response.StatusCode;
        problem.Status = status;
        var (defaultTitle, defaultCode) = ForStatus(status);

        if (!problem.Extensions.ContainsKey(CodeKey))
            problem.Extensions[CodeKey] = context.Exception is DomainException domain ? CodeFor(domain)
                : status == 404 && IsUnknownRoute(http) ? "route_not_found"
                : defaultCode;

        if (string.IsNullOrWhiteSpace(problem.Title) || !HasCyrillic(problem.Title))
            problem.Title = defaultTitle;

        if (status >= 500 && context.Exception is not DomainException)
            problem.Detail = InternalDetail;
        else if (string.IsNullOrWhiteSpace(problem.Detail) || !HasCyrillic(problem.Detail))
            problem.Detail = status switch
            {
                404 when IsUnknownRoute(http) => "Такого адреса нет",
                405 => "Метод не поддерживается для этого адреса",
                413 => "Размер запроса превышает допустимый",
                415 => "Отправьте данные в формате JSON",
                _ => problem.Title
            };

        // Внутренности исключений наружу не отдаются (в Development ASP.NET добавил бы их в extensions).
        problem.Extensions.Remove("exception");
        // Тот же идентификатор, что TraceId в записи лога о запросе (Serilog, поле @tr): по нему ошибку находят в логах.
        problem.Extensions[TraceIdKey] = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier;

        if (IsTajik(http))
        {
            problem.Title = ErrorCatalog.Translate(problem.Title!, true);
            problem.Detail = ErrorCatalog.Translate(problem.Detail!, true);
            if (problem is HttpValidationProblemDetails validation)
                foreach (var key in validation.Errors.Keys.ToList())
                    validation.Errors[key] = validation.Errors[key].Select(m => ErrorCatalog.Translate(m, true)).ToArray();
        }
    }

    // Ответ 400 при ошибках модели: и для встроенной проверки ASP.NET, и для FluentValidation.
    // Ошибки разбора JSON — одна ошибка «Некорректный формат запроса» без текста парсера. Английское
    // «The X field is required.» убирается, если у поля есть своё сообщение валидатора, иначе заменяется русским.
    // Ключи полей — camelCase, как в JSON запроса.
    public static IActionResult ValidationResult(ActionContext context)
    {
        var modelState = context.ModelState;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var malformed = modelState.Any(e => e.Key.StartsWith('$') && e.Value?.Errors.Count > 0)
                        || modelState.Values.Any(v => v.Errors.Any(err => err.Exception is not null));

        if (malformed)
            errors["body"] = [MalformedRequest];
        else
        {
            foreach (var (key, entry) in modelState)
            {
                if (entry.ValidationState != ModelValidationState.Invalid || entry.Errors.Count == 0)
                    continue;
                var messages = entry.Errors.Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                var russian = messages.Where(HasCyrillic).Distinct().ToList();
                if (russian.Count == 0)
                    russian = [RequiredMessage(key)];
                errors[CamelCase(key)] = russian.ToArray();
            }
            // Нет тела запроса вовсе: ошибка без поля («A non-empty request body is required») или по параметру «request».
            if (errors.ContainsKey("") || (errors.Count == 1 && errors.ContainsKey("request")))
            {
                errors.Clear();
                errors["body"] = [MalformedRequest];
                malformed = true;
            }
        }

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = ValidationTitle,
            Detail = malformed ? MalformedRequest : errors.Values.SelectMany(v => v).FirstOrDefault() ?? ValidationTitle
        };
        problem.Extensions[CodeKey] = malformed ? "malformed_request" : "validation_failed";
        // traceId и перевод на таджикский — тем же общим шагом, что у остальных ошибок.
        Customize(new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem });
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static string RequiredMessage(string key)
    {
        var field = key.Split('.', '[').LastOrDefault(part => part.Length > 0)?.TrimEnd(']');
        return field is not null && ValidationSetup.DisplayName(field) is { } name ? $"Заполните поле «{name}»" : "Заполните обязательное поле";
    }

    private static string CamelCase(string key) => string.Join('.', key.Split('.').Select(part =>
        part.Length == 0 ? part : JsonNamingPolicy.CamelCase.ConvertName(part)));

    // Адрес не совпал ни с одним маршрутом API: эндпоинта нет или сработал запасной маршрут сайта (MapFallback).
    private static bool IsUnknownRoute(HttpContext http) =>
        http.GetEndpoint() is not { } endpoint || endpoint.DisplayName?.StartsWith("Fallback", StringComparison.Ordinal) == true;

    public static bool HasCyrillic(string? text) => text is not null && CyrillicRegex().IsMatch(text);

    [GeneratedRegex(@"\p{IsCyrillic}")]
    private static partial Regex CyrillicRegex();
}

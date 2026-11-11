using System.Text.RegularExpressions;
using Sentry;
using Serilog;
using Serilog.Events;

namespace Sitora.WebApi.Extensions;

// Ошибки в Sentry (или совместимый сервис, например GlitchTip): включается переменной Sentry__Dsn, без неё ничего не
// отправляется. Уходят только записи уровня Error и выше, без персональных данных: токены, cookie, строка запроса,
// почты, телефоны и номера паспортов вычищаются до отправки.
public static partial class SentrySetup
{
    public static LoggerConfiguration WriteToSentryIfConfigured(this LoggerConfiguration logger, IConfiguration configuration, IHostEnvironment environment)
    {
        var dsn = configuration["Sentry:Dsn"];
        if (string.IsNullOrWhiteSpace(dsn))
            return logger;

        return logger.WriteTo.Sentry(o =>
        {
            o.Dsn = dsn;
            o.Environment = environment.EnvironmentName;
            o.MinimumEventLevel = LogEventLevel.Error;
            o.MinimumBreadcrumbLevel = LogEventLevel.Information;
            o.SendDefaultPii = false;
            o.AttachStacktrace = true;
            o.SetBeforeSend((e, _) => Scrub(e));
            o.SetBeforeBreadcrumb((b, _) => b.Message is null ? b : new Breadcrumb(Mask(b.Message), b.Type ?? "default", b.Data?.ToDictionary(kv => kv.Key, kv => Mask(kv.Value)), b.Category, b.Level));
        });
    }

    public static SentryEvent Scrub(SentryEvent e)
    {
        e.User = new SentryUser();
        e.Request.Cookies = null;
        e.Request.QueryString = null;
        e.Request.Data = null;
        foreach (var header in e.Request.Headers.Keys.Where(k => k is "Authorization" or "Cookie" or "X-Health-Key").ToList())
            e.Request.Headers.Remove(header);
        if (e.Message?.Formatted is { } formatted)
            e.Message = new SentryMessage { Formatted = Mask(formatted), Message = e.Message.Message };
        foreach (var exception in e.SentryExceptions ?? [])
            exception.Value = exception.Value is null ? null : Mask(exception.Value);
        return e;
    }

    // Почта, телефон (от 9 цифр), JWT и длинные токены, серия и номер паспорта (буква + 7–9 цифр) → [скрыто].
    public static string Mask(string text) => SecretsRegex().Replace(text, "[скрыто]");

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+|\+?\d[\d\s()-]{8,}\d|eyJ[\w-]+\.[\w-]+\.[\w-]+|\b[A-Za-z0-9_-]{40,}\b|\b[A-ZА-Я]{1,2}\s?\d{7,9}\b")]
    private static partial Regex SecretsRegex();
}

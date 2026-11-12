using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Integrations;

// Какие способы входа по коду реально работают — письмо или SMS с кодом доходит до человека. Без провайдера код
// никуда не уходит (Dev и заглушка SMS пишут его в лог только в Development), и вместо «код отправлен», который
// не придёт, API честно отвечает 503 (email_not_configured / sms_not_configured), а GET /auth/methods говорит сайту,
// какой способ предлагать.
public class LoginChannels(IConfiguration configuration, IHostEnvironment environment, ISmsSender sms, IOptions<AuthOptions> auth)
{
    public const string EmailNotConfigured = "Письма с кодами не отправляются: почтовый сервис не настроен";
    public const string SmsNotConfigured = "SMS с кодами пока не отправляются. Войдите по почте";
    public const string EmailNotConfiguredLog = "Письма с кодами не отправляются: задайте Email__Provider (Smtp или Resend)";

    // Почта: в Development коды видны в логе; в остальных окружениях нужен настоящий провайдер (Smtp или Resend).
    public bool EmailWorks => EmailWorksFor(configuration, environment);

    // SMS: пока подключена только заглушка — она «отправляет» лишь в Development (код в логе). Настоящий провайдер
    // (другая реализация ISmsSender) работает везде. Вход по SMS ещё и включается флагом Auth:SmsLoginEnabled.
    public bool SmsProviderConnected => sms is not SmsSender || environment.IsDevelopment();

    public bool SmsWorks => auth.Value.SmsLoginEnabled && SmsProviderConnected;

    public static bool EmailWorksFor(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return true;
        var provider = EmailOptions.HasSmtpSection(configuration) ? "smtp" : configuration[$"{EmailOptions.SectionName}:Provider"];
        return provider?.Trim().ToLowerInvariant() is "smtp" or "resend";
    }
}

// /health/ready: письма с кодами уходят. Без почтового провайдера вне Development — Degraded (API работает,
// но покупатели не получат коды входа, а сотрудники — приглашения и сброс пароля).
public class EmailHealthCheck(IConfiguration configuration, IHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(LoginChannels.EmailWorksFor(configuration, environment)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded(LoginChannels.EmailNotConfiguredLog));
}

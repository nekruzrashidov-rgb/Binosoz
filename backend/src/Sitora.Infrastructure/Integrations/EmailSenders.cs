using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Настройки почты (раздел Email; в .env — переменные Email__*). Ключи и пароли — только из окружения.
public class EmailOptions
{
    public const string SectionName = "Email";

    // Dev — письма в лог (только для разработки), Smtp или Resend.
    public string Provider { get; set; } = "Dev";

    [EmailAddress]
    public string From { get; set; } = "no-reply@sitora.local";

    public string FromName { get; set; } = "Binosoz";

    // Короткие переменные Smtp__Host, Smtp__Port, Smtp__Username, Smtp__Password, Smtp__FromEmail, Smtp__FromName
    // (user-secrets или .env) перекрывают Email__Smtp__* и включают отправку через SMTP.
    public const string SmtpSectionName = "Smtp";

    public static bool HasSmtpSection(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[$"{SmtpSectionName}:Host"]);

    public static void ApplySmtpSection(EmailOptions options, IConfiguration configuration)
    {
        if (!HasSmtpSection(configuration))
            return;
        var smtp = configuration.GetSection(SmtpSectionName);
        options.Provider = "Smtp";
        options.Smtp.Host = smtp["Host"];
        if (int.TryParse(smtp["Port"], out var port))
            options.Smtp.Port = port;
        options.Smtp.Username = smtp["Username"] ?? options.Smtp.Username;
        options.Smtp.Password = smtp["Password"] ?? options.Smtp.Password;
        if (!string.IsNullOrWhiteSpace(smtp["FromEmail"]))
            options.From = smtp["FromEmail"]!;
        if (!string.IsNullOrWhiteSpace(smtp["FromName"]))
            options.FromName = smtp["FromName"]!;
    }

    // Адрес страницы принятия приглашения; токен дописывается в конец.
    public string InviteUrl { get; set; } = "http://localhost:5500/accept-invite#token=";

    // Страница входа на том же сайте, что и приглашения: для писем «входите по коду».
    public string LoginUrl => Uri.TryCreate(InviteUrl, UriKind.Absolute, out var url)
        ? url.GetLeftPart(UriPartial.Authority) + "/login"
        : InviteUrl.Split('#')[0];

    public SmtpSettings Smtp { get; set; } = new();
    public ResendSettings Resend { get; set; } = new();
    public GmailSettings Gmail { get; set; } = new();

    public class SmtpSettings
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool UseSsl { get; set; } = true;
    }

    public class ResendSettings
    {
        public string? ApiKey { get; set; }
        public string BaseUrl { get; set; } = "https://api.resend.com";
    }

    // Gmail API (HTTPS, порт 443 — работает и там, где исходящий SMTP закрыт, например на бесплатном Render).
    // Email__Gmail__ClientId, Email__Gmail__ClientSecret, Email__Gmail__RefreshToken — OAuth-клиент типа Desktop app
    // и refresh token владельца ящика со scope gmail.send (backend/scripts/gmail-token.mjs). Значения — только из окружения.
    public class GmailSettings
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? RefreshToken { get; set; }
        public string TokenUrl { get; set; } = "https://oauth2.googleapis.com/token";
        public string ApiBaseUrl { get; set; } = "https://gmail.googleapis.com";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RefreshToken);
    }
}

// Для разработки: письмо не уходит. В Development текст с кодом пишется в лог, чтобы можно было войти;
// в других окружениях код в лог не попадает (иначе его прочитал бы любой с доступом к логам).
public class DevEmailSender(IHostEnvironment environment, ILogger<DevEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (environment.IsDevelopment())
            logger.LogInformation("[Почта, разработка] {To}: {Subject}\n{Text}", message.To, message.Subject, message.Text);
        else
            logger.LogWarning("[Почта] Письмо «{Subject}» не отправлено: почтовый сервис не настроен (Email__Provider)", message.Subject);
        return Task.CompletedTask;
    }
}

public class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Smtp.Host))
            throw new InvalidOperationException("Не задан SMTP-сервер (Email__Smtp__Host)");

        using var mail = new MailMessage(new MailAddress(o.From, o.FromName), new MailAddress(message.To))
        {
            Subject = message.Subject,
            Body = message.Text,
            IsBodyHtml = false
        };
        if (message.Html is not null)
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, System.Text.Encoding.UTF8, "text/html"));
        using var client = new SmtpClient(o.Smtp.Host, o.Smtp.Port) { EnableSsl = o.Smtp.UseSsl };
        if (!string.IsNullOrEmpty(o.Smtp.Username))
            client.Credentials = new NetworkCredential(o.Smtp.Username, o.Smtp.Password);
        await client.SendMailAsync(mail, ct);
    }
}

// Resend (resend.com): HTTP API, ключ в Email__Resend__ApiKey.
public class ResendEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender
{
    // Письмо без HTML уходит без поля html, а не с html: null.
    private static readonly System.Text.Json.JsonSerializerOptions OmitNulls =
        new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Resend.ApiKey))
            throw new InvalidOperationException("Не задан ключ Resend (Email__Resend__ApiKey)");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.Resend.BaseUrl), "/emails"))
        {
            Content = JsonContent.Create(new
            {
                from = $"{o.FromName} <{o.From}>",
                to = new[] { message.To },
                subject = message.Subject,
                text = message.Text,
                html = message.Html
            }, options: OmitNulls)
        };
        request.Headers.Authorization = new("Bearer", o.Resend.ApiKey);
        using var response = await http.SendAsync(request, ct);
        // Текст ответа Resend объясняет причину (например, домен отправителя не подтверждён); ключа в нём нет.
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Resend не принял письмо: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
    }
}

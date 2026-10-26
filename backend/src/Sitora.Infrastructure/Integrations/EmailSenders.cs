using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
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

    public string FromName { get; set; } = "Sitora";

    // Адрес страницы принятия приглашения; токен дописывается в конец.
    public string InviteUrl { get; set; } = "http://localhost:5500/#invite=";

    public SmtpSettings Smtp { get; set; } = new();
    public ResendSettings Resend { get; set; } = new();

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
        using var client = new SmtpClient(o.Smtp.Host, o.Smtp.Port) { EnableSsl = o.Smtp.UseSsl };
        if (!string.IsNullOrEmpty(o.Smtp.Username))
            client.Credentials = new NetworkCredential(o.Smtp.Username, o.Smtp.Password);
        await client.SendMailAsync(mail, ct);
    }
}

// Resend (resend.com): HTTP API, ключ в Email__Resend__ApiKey.
public class ResendEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender
{
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
                text = message.Text
            })
        };
        request.Headers.Authorization = new("Bearer", o.Resend.ApiKey);
        using var response = await http.SendAsync(request, ct);
        // Текст ответа Resend объясняет причину (например, домен отправителя не подтверждён); ключа в нём нет.
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Resend не принял письмо: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
    }
}

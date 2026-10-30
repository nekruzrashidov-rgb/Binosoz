using System.Net;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Оформленные письма. Только встроенные стили и белый фон: почтовые программы не грузят CSS и тёмные темы.
// Всё, что ввёл человек, экранируется.
public static class EmailTemplates
{
    public static EmailMessage ApplicationReceived(string to, string ownerFullName, string companyName)
    {
        var text = $"Здравствуйте, {ownerFullName}!\n\n" +
                   $"Мы получили заявку от компании «{companyName}». Наша команда рассмотрит её в течение 1–2 рабочих дней " +
                   "и свяжется с вами по этому адресу.\n\nЕсли срочно — напишите в Telegram: https://t.me/nekru01\n\n" +
                   "© 2026 Binosoz · Душанбе, Таджикистан";

        var owner = WebUtility.HtmlEncode(ownerFullName);
        var company = WebUtility.HtmlEncode(companyName);
        var html = $"""
            <div style="background:#ffffff; font-family: Arial, sans-serif; max-width:560px; margin:0 auto; padding:40px 24px;">
              <div style="text-align:center; margin-bottom:32px;">
                <span style="font-size:32px;">⭐</span>
                <h1 style="color:#1a1a2e; font-size:24px; margin:8px 0;">Binosoz</h1>
              </div>
              <h2 style="color:#1a1a2e; font-size:20px;">Заявка принята!</h2>
              <p style="color:#555; line-height:1.6;">Здравствуйте, <strong>{owner}</strong>!</p>
              <p style="color:#555; line-height:1.6;">
                Мы получили заявку от компании <strong>{company}</strong>.
                Наша команда рассмотрит её в течение <strong>1–2 рабочих дней</strong>
                и свяжется с вами по этому адресу.
              </p>
              <div style="background:#f8f9fa; border-left:4px solid #c9a227; padding:16px; margin:24px 0; border-radius:4px;">
                <p style="margin:0; color:#333;">
                  📞 Если срочно — напишите в Telegram:
                  <a href="https://t.me/nekru01" style="color:#c9a227;">@nekru01</a>
                </p>
              </div>
              <p style="color:#999; font-size:13px; margin-top:32px;">© 2026 Binosoz · Душанбе, Таджикистан</p>
            </div>
            """;
        return new EmailMessage(to, "Заявка принята — Binosoz", text, html);
    }
}

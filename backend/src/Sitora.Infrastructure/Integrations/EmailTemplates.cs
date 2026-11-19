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

    // Напоминание о платеже покупателю: на его языке, от имени застройщика (brand), кнопка «Открыть кабинет»
    // и ссылка «Отписаться» (без входа). Только сумма, номер квартиры и срок — без паспортных данных.
    public static EmailMessage PaymentReminder(string to, string lang, string brand, string name, string title, string body,
        string? cabinetUrl, string unsubscribeUrl)
    {
        var intro = Services.BuyerTexts.EmailIntro(lang, name);
        var paid = Services.BuyerTexts.AlreadyPaid(lang);
        var footer = Services.BuyerTexts.EmailFooter(lang, brand);
        var open = Services.BuyerTexts.OpenCabinet(lang);
        var unsubscribe = Services.BuyerTexts.Unsubscribe(lang);
        var text = $"{intro}\n\n{body}.\n\n" + (cabinetUrl is null ? "" : $"{open}: {cabinetUrl}\n\n") +
                   $"{paid}\n\n— {brand}\n{footer}\n{unsubscribe}: {unsubscribeUrl}";

        string E(string s) => WebUtility.HtmlEncode(s);
        var button = cabinetUrl is null ? "" : $"""
              <p style="margin:28px 0;"><a href="{E(cabinetUrl)}" style="display:inline-block; background:#8a5a2b; color:#ffffff; text-decoration:none; padding:14px 24px; border-radius:2px; font-weight:bold;">{E(open)}</a></p>
            """;
        var html = $"""
            <div style="background:#ffffff; font-family: Arial, sans-serif; max-width:560px; margin:0 auto; padding:32px 24px; color:#222222;">
              <p style="font-size:13px; color:#666666; margin:0 0 24px;">{E(brand)}</p>
              <h1 style="font-size:22px; margin:0 0 16px; color:#1a1a1a;">{E(title)}</h1>
              <p style="line-height:1.6;">{E(intro)}</p>
              <p style="line-height:1.6; font-size:17px;"><strong>{E(body)}</strong></p>
              {button}
              <p style="line-height:1.6; color:#555555;">{E(paid)}</p>
              <p style="color:#555555; margin-top:32px;">— {E(brand)}</p>
              <p style="font-size:12px; color:#777777; line-height:1.5;">{E(footer)}<br><a href="{E(unsubscribeUrl)}" style="color:#777777;">{E(unsubscribe)}</a></p>
            </div>
            """;
        return new EmailMessage(to, $"{brand}: {title}", text, html, brand);
    }
}

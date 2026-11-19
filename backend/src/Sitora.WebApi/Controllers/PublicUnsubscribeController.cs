using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Services;

namespace Sitora.WebApi.Controllers;

// «Отписаться» из письма с напоминанием: без входа, по подписанной ссылке (90 дней). GET показывает страницу
// с кнопкой — почтовые сканеры, открывающие ссылки, ничего не выключат; POST выключает письма (Telegram и кабинет остаются).
[ApiController]
[Route("api/v1/public/unsubscribe")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class PublicUnsubscribeController(AppDbContext db, UnsubscribeTokens tokens) : ControllerBase
{
    [HttpGet]
    public async Task<ContentResult> Ask([FromQuery] string? t, CancellationToken ct)
    {
        var lang = await LangAsync(t, ct);
        return tokens.TryRead(t, out _)
            ? Page(lang, BuyerTexts.UnsubscribeAsk(lang), t, StatusCodes.Status200OK)
            : Page(lang, BuyerTexts.UnsubscribeBad(lang), null, StatusCodes.Status404NotFound);
    }

    [HttpPost]
    public async Task<ContentResult> Confirm([FromQuery] string? t, CancellationToken ct)
    {
        var lang = await LangAsync(t, ct);
        if (!tokens.TryRead(t, out var userId))
            return Page(lang, BuyerTexts.UnsubscribeBad(lang), null, StatusCodes.Status404NotFound);
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.NotifyByEmail, false), ct);
        return Page(lang, BuyerTexts.UnsubscribeDone(lang), null, StatusCodes.Status200OK);
    }

    private async Task<string> LangAsync(string? token, CancellationToken ct)
    {
        if (tokens.TryRead(token, out var userId))
        {
            var saved = await db.Users.Where(u => u.Id == userId).Select(u => u.NotifyLanguage).FirstOrDefaultAsync(ct);
            if (saved is not null)
                return BuyerTexts.Lang(saved);
        }
        return BuyerTexts.FromHeader(Request.Headers.AcceptLanguage);
    }

    // Простая страница без скриптов и встроенных стилей (CSP API). Кнопка — обычная форма POST на этот же адрес.
    private static ContentResult Page(string lang, string text, string? token, int status)
    {
        var title = WebUtility.HtmlEncode(BuyerTexts.Unsubscribe(lang));
        var button = token is null ? "" :
            $"<form method=\"post\" action=\"?t={WebUtility.UrlEncode(token)}\"><button type=\"submit\">{title}</button></form>";
        var html = $"<!doctype html><html lang=\"{lang}\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                   $"<meta name=\"robots\" content=\"noindex\"><title>{title}</title></head>" +
                   $"<body><main><p>{WebUtility.HtmlEncode(text)}</p>{button}</main></body></html>";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}

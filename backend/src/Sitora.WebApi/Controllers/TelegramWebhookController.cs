using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Services;

namespace Sitora.WebApi.Controllers;

// Webhook бота: Telegram присылает сюда сообщения пользователей (/start <токен>, /stop).
// Секрет Telegram:WebhookSecret — и в адресе, и в заголовке X-Telegram-Bot-Api-Secret-Token (его ставит setWebhook
// с secret_token). Нет секрета, не совпал адрес или заголовок — 404, как будто адреса нет.
[ApiController]
[Route("api/v1/telegram/webhook")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class TelegramWebhookController(TelegramLinkService links, IOptions<TelegramOptions> options, ILogger<TelegramWebhookController> logger) : ControllerBase
{
    public const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    [HttpPost("{secret}")]
    public async Task<IActionResult> Receive(string secret, [FromBody] JsonElement update, CancellationToken ct)
    {
        var expected = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(expected) || !Same(secret, expected) || !Same(Request.Headers[SecretHeader].ToString(), expected))
            return NotFound();
        try
        {
            await links.HandleUpdateAsync(update, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Ответ 200 всё равно: иначе Telegram будет повторять то же обновление.
            logger.LogError(e, "Не удалось обработать сообщение бота");
        }
        return Ok();
    }

    private static bool Same(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

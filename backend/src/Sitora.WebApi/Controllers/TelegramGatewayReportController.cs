using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.WebApi.Controllers;

// Отчёты Telegram Gateway о доставке кода (callback_url = Telegram__Gateway__CallbackUrl): подпись
// X-Request-Timestamp + X-Request-Signature проверяется ключом от токена; неверная подпись — 404.
// Статус («delivered», «read», «expired»…) пишется к коду входа — по нему видно, доходят ли коды.
[ApiController]
[Route("api/v1/telegram/gateway/report")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class TelegramGatewayReportController(AppDbContext db, ITelegramGateway gateway) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Report(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        if (!gateway.IsValidReport(Request.Headers["X-Request-Timestamp"], Request.Headers["X-Request-Signature"], body))
            return NotFound();
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var requestId = root.TryGetProperty("request_id", out var id) ? id.GetString() : null;
            var status = root.TryGetProperty("delivery_status", out var d) && d.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (requestId is not null && status is not null)
                await db.SmsCodes.Where(c => c.GatewayRequestId == requestId)
                    .ExecuteUpdateAsync(x => x.SetProperty(c => c.DeliveryStatus, status.Length > 32 ? status[..32] : status), ct);
        }
        catch (JsonException)
        {
            return BadRequest();
        }
        return Ok();
    }
}

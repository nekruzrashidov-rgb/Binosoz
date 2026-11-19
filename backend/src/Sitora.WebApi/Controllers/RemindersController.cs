using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Exceptions;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Services;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// Напоминания о платежах в CRM: журнал по сделке и «Напомнить сейчас». Сделка чужого дома или организации — 404.
[ApiController]
[Route("api/v1/deals/{dealId:guid}/reminders")]
[Authorize(Roles = Roles.DealStaff)]
[Produces("application/json")]
public class RemindersController(AppDbContext db, DealAccess access, PaymentAlertService alerts) : ControllerBase
{
    /// <summary>
    /// Журнал: когда и по какому каналу (InApp — кабинет, Telegram, Email) ушло сообщение покупателю по этой сделке
    /// и чем кончилось (Pending, Sent, Failed, Skipped). Последние 200, новые сверху.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReminderLogItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<ReminderLogItem>> Get(Guid dealId, CancellationToken ct)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException("Сделка не найдена");
        var info = await db.Deals.AsNoTracking().Where(d => d.Id == dealId)
            .Select(d => new { d.Customer.FullName, d.Apartment.Number }).SingleAsync(ct);
        var rows = await db.BuyerMessages.AsNoTracking().Where(m => m.DealId == dealId)
            .OrderByDescending(m => m.CreatedAt).Take(200)
            .Select(m => new
            {
                m.Id, m.Event, m.Channel, m.Status, m.DueDate, m.CreatedAt, m.SentAt, m.Error,
                CreatedBy = m.CreatedById == null ? null : db.Users.Where(u => u.Id == m.CreatedById).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(ct);
        return rows.Select(m => new ReminderLogItem(m.Id, m.Event, m.Channel, m.Status, m.DueDate, m.CreatedAt, m.SentAt, m.Error,
            m.CreatedBy, info.FullName, info.Number)).ToList();
    }

    /// <summary>
    /// «Напомнить сейчас» по пункту графика (dueDate): кабинет, Telegram и почта — по настройкам покупателя.
    /// Не чаще раза в сутки на пункт — иначе 429 reminder_too_soon; оплаченный пункт — 422; чужая сделка — 404.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<RemindNowResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<RemindNowResponse> RemindNow(Guid dealId, RemindNowRequest request, CancellationToken ct)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException("Сделка не найдена");
        return new RemindNowResponse(await alerts.RemindNowAsync(dealId, request.DueDate, User.GetUserId(), ct));
    }
}

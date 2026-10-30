using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class ChatService(
    AppDbContext db,
    DealAccess access,
    ICurrentUser currentUser,
    IChatNotifier chatNotifier,
    IPresenceTracker presence,
    INotificationSender notifications,
    TimeProvider timeProvider,
    ILogger<ChatService> logger) : IChatService
{
    private const string DealNotFound = "Сделка не найдена";
    private const int MaxConversations = 200;
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);

    public Task<bool> CanAccessAsync(Guid dealId, CancellationToken ct = default) => access.CanViewAsync(dealId, ct);

    public async Task<PagedResult<ChatMessageResponse>> GetMessagesAsync(Guid dealId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException(DealNotFound);

        // Читающий видит сообщения другой стороны — они становятся прочитанными.
        var readerIsCustomer = IsCustomerSide;
        await db.ChatMessages
            .Where(m => m.DealId == dealId && m.ReadAt == null && m.FromCustomer != readerIsCustomer)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, timeProvider.GetUtcNow().UtcDateTime), ct);

        var p = PageRequest.Normalize(page, pageSize);
        var query = db.ChatMessages.AsNoTracking().Where(m => m.DealId == dealId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip(p.Skip).Take(p.PageSize)
            .Select(m => new ChatMessageResponse(m.Id, m.DealId, m.SenderId, m.Sender.FullName, m.FromCustomer, m.Text, m.CreatedAt, m.ReadAt))
            .ToListAsync(ct);
        return new PagedResult<ChatMessageResponse>(items, p.Page, p.PageSize, total);
    }

    public async Task<ChatUnreadResponse> GetUnreadAsync(Guid dealId, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException(DealNotFound);

        var readerIsCustomer = IsCustomerSide;
        var unread = await db.ChatMessages.CountAsync(m => m.DealId == dealId && m.ReadAt == null && m.FromCustomer != readerIsCustomer, ct);
        var manager = await db.Deals.AsNoTracking().Where(d => d.Id == dealId)
            .Select(d => new { d.ManagerId, d.Manager.FullName })
            .SingleAsync(ct);
        var lastSeen = presence.LastSeen(manager.ManagerId);
        var online = lastSeen is { } seen && timeProvider.GetUtcNow().UtcDateTime - seen <= OnlineWindow;
        return new ChatUnreadResponse(unread, manager.FullName, online, lastSeen);
    }

    public async Task<IReadOnlyList<ChatConversationResponse>> GetConversationsAsync(string? search, CancellationToken ct = default)
    {
        // Сделки видны по фильтрам организации и домов сотрудника — те же, что в списке сделок.
        var deals = db.Deals.AsNoTracking().Where(d => db.ChatMessages.Any(m => m.DealId == d.Id));
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var lower = term.ToLower();
            deals = digits.Length >= 3
                ? deals.Where(d => d.Customer.Phone.Contains(digits) || d.Customer.FullName.ToLower().Contains(lower))
                : deals.Where(d => d.Customer.FullName.ToLower().Contains(lower));
        }

        var rows = await deals
            .Select(d => new
            {
                d.Id,
                d.Customer.FullName,
                d.Customer.Phone,
                d.Apartment.Number,
                d.Apartment.Floor.Building.NameRu,
                d.Apartment.Floor.Building.NameTg,
                Last = db.ChatMessages.Where(m => m.DealId == d.Id).OrderByDescending(m => m.CreatedAt)
                    .Select(m => new { m.Text, m.CreatedAt, m.FromCustomer }).First(),
                Unread = db.ChatMessages.Count(m => m.DealId == d.Id && m.FromCustomer && m.ReadAt == null)
            })
            .OrderByDescending(x => x.Last.CreatedAt)
            .Take(MaxConversations)
            .ToListAsync(ct);
        var tg = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tg";
        return rows.Select(r => new ChatConversationResponse(r.Id, r.FullName, r.Phone, r.Number, tg ? r.NameTg : r.NameRu,
            r.Last.Text, r.Last.CreatedAt, r.Last.FromCustomer, r.Unread)).ToList();
    }

    public async Task<ChatMessageResponse> SendAsync(Guid dealId, string text, CancellationToken ct = default)
    {
        var senderId = currentUser.UserId ?? throw new UnauthorizedException("Требуется вход");
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException(DealNotFound);

        text = text?.Trim() ?? string.Empty;
        if (text.Length is 0 or > ChatMessage.MaxLength)
            throw new BusinessRuleException($"Сообщение должно быть от 1 до {ChatMessage.MaxLength} символов");

        var message = new ChatMessage { DealId = dealId, SenderId = senderId, FromCustomer = IsCustomerSide, Text = text };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var info = await db.Deals.AsNoTracking().Where(d => d.Id == dealId)
            .Select(d => new { CustomerUserId = d.Customer.UserId, ApartmentNumber = d.Apartment.Number })
            .SingleAsync(ct);
        var senderName = await db.Users.Where(u => u.Id == senderId).Select(u => u.FullName).SingleAsync(ct);
        var response = new ChatMessageResponse(message.Id, dealId, senderId, senderName, message.FromCustomer, message.Text, message.CreatedAt, null);

        await SafeAsync(() => chatNotifier.MessageCreatedAsync(response, ct), "доставить сообщение в реальном времени");

        // Второй стороне — уведомление: покупателю лично, сотрудникам — в общий канал менеджеров.
        var recipient = message.FromCustomer ? null : info.CustomerUserId;
        if (message.FromCustomer || recipient is not null)
        {
            await SafeAsync(() => notifications.SendAsync(recipient,
                $"Новое сообщение по кв. №{info.ApartmentNumber} от {senderName}: {Shorten(text)}", ct), "отправить уведомление о сообщении");
        }

        return response;
    }

    // Сотрудник, который одновременно покупатель, пишет как сотрудник.
    private bool IsCustomerSide => currentUser.IsInRole(RoleNames.Customer) && !currentUser.IsDealStaff;

    private async Task SafeAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось {What}", what);
        }
    }

    private static string Shorten(string text) => text.Length <= 100 ? text : text[..100] + "…";
}

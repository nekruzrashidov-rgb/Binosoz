using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Controllers;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Hubs;

// Чат по сделке в реальном времени. Клиент вызывает JoinDeal(dealId) и получает
// событие "messageReceived" с ChatMessageResponse на каждое новое сообщение.
// Отправлять можно и через хаб (SendMessage), и через POST /deals/{id}/messages.
[Authorize(Roles = Roles.DealViewers)]
public class ChatHub(IChatService chat, HubCaller caller) : Hub
{
    public const string MessageReceived = "messageReceived";

    public static string GroupName(Guid dealId) => $"deal:{dealId:N}";

    public async Task JoinDeal(Guid dealId)
    {
        caller.Principal = Context.User;
        if (!await chat.CanAccessAsync(dealId, Context.ConnectionAborted))
            throw new HubException("Сделка не найдена");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(dealId), Context.ConnectionAborted);
    }

    public Task LeaveDeal(Guid dealId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(dealId), Context.ConnectionAborted);

    public async Task<ChatMessageResponse> SendMessage(Guid dealId, string text)
    {
        caller.Principal = Context.User;
        try
        {
            return await chat.SendAsync(dealId, text, Context.ConnectionAborted);
        }
        catch (DomainException e)
        {
            // Текст HubException доходит до клиента; остальные ошибки SignalR скрывает.
            throw new HubException(e.Message);
        }
    }
}

public class SignalRChatNotifier(IHubContext<ChatHub> hub) : IChatNotifier
{
    public Task MessageCreatedAsync(ChatMessageResponse message, CancellationToken ct = default) =>
        hub.Clients.Group(ChatHub.GroupName(message.DealId)).SendAsync(ChatHub.MessageReceived, message, ct);
}

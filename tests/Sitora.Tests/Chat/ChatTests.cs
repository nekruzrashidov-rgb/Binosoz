using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Common;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Chat;

public class ChatTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ManagerAndCustomer_ExchangeMessagesInRealTime()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var customerClient = await factory.CreateCustomerClientAsync(deal.Phone);
        var managerClient = await factory.CreateManagerClientAsync();

        await using var customerHub = Connect(Token(customerClient));
        await using var managerHub = Connect(Token(managerClient));
        var managerReceived = new TaskCompletionSource<ChatMessageResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var customerReceived = new TaskCompletionSource<ChatMessageResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        managerHub.On<ChatMessageResponse>("messageReceived", m => { if (m.FromCustomer) managerReceived.TrySetResult(m); });
        customerHub.On<ChatMessageResponse>("messageReceived", m => { if (!m.FromCustomer) customerReceived.TrySetResult(m); });

        await customerHub.StartAsync();
        await managerHub.StartAsync();
        await customerHub.InvokeAsync("JoinDeal", deal.DealId);
        await managerHub.InvokeAsync("JoinDeal", deal.DealId);

        // Покупатель пишет через хаб — менеджер получает сразу.
        var sent = await customerHub.InvokeAsync<ChatMessageResponse>("SendMessage", deal.DealId, "Здравствуйте! Когда сдача дома?");
        var atManager = await managerReceived.Task.WaitAsync(Timeout);
        Assert.Equal(sent.Id, atManager.Id);
        Assert.Equal("Здравствуйте! Когда сдача дома?", atManager.Text);
        Assert.Equal("Покупатель Кабинета", atManager.SenderName);

        // Менеджер отвечает через REST — покупатель тоже получает сразу.
        var reply = await managerClient.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/messages", new SendMessageRequest("Планируем во втором квартале"));
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        var atCustomer = await customerReceived.Task.WaitAsync(Timeout);
        Assert.Equal("Планируем во втором квартале", atCustomer.Text);
        Assert.False(atCustomer.FromCustomer);
    }

    [Fact]
    public async Task Hub_RejectsForeignDeal_AndStaffWithoutDealAccess()
    {
        var mine = await TestScenarios.CreateDealAsync(factory);
        var foreign = await TestScenarios.CreateDealAsync(factory);
        await using var hub = Connect(Token(await factory.CreateCustomerClientAsync(mine.Phone)));
        await hub.StartAsync();

        var join = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("JoinDeal", foreign.DealId));
        Assert.Contains("Сделка не найдена", join.Message);
        var send = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync<ChatMessageResponse>("SendMessage", foreign.DealId, "чужое"));
        Assert.Contains("Сделка не найдена", send.Message);
        var empty = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync<ChatMessageResponse>("SendMessage", mine.DealId, "   "));
        Assert.Contains("от 1 до", empty.Message);

        await using var contentHub = Connect(Token(await factory.CreateContentClientAsync()));
        await Assert.ThrowsAnyAsync<Exception>(() => contentHub.StartAsync());
    }

    [Fact]
    public async Task Rest_HistoryMarksOtherSideAsRead_AndNotifies()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var manager = await factory.CreateManagerClientAsync();

        var question = await TestScenarios.ReadAsync<ChatMessageResponse>(
            await customer.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/messages", new SendMessageRequest("Можно посмотреть квартиру в субботу?")));
        Assert.True(question.FromCustomer);

        // Покупатель сам себе сообщение «прочитанным» не делает.
        await customer.GetAsync($"/api/v1/deals/{deal.DealId}/messages");
        Assert.Null(await ReadAtAsync(question.Id));

        var history = await manager.GetFromJsonAsync<PagedResult<ChatMessageResponse>>($"/api/v1/deals/{deal.DealId}/messages", TestJson.Options);
        Assert.Equal(question.Id, Assert.Single(history!.Items).Id);
        Assert.NotNull(await ReadAtAsync(question.Id));

        var answer = await TestScenarios.ReadAsync<ChatMessageResponse>(
            await manager.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/messages", new SendMessageRequest("Да, в 11:00 ждём вас")));
        var forCustomer = await customer.GetFromJsonAsync<PagedResult<ChatMessageResponse>>($"/api/v1/deals/{deal.DealId}/messages", TestJson.Options);
        Assert.Equal([answer.Id, question.Id], forCustomer!.Items.Select(m => m.Id));
        Assert.NotNull(await ReadAtAsync(answer.Id));

        var customerUserId = await factory.WithDbAsync(db => db.Customers.Where(c => c.Id == deal.CustomerId).Select(c => c.UserId).SingleAsync());
        var toManagers = await factory.WithDbAsync(db => db.Notifications.AnyAsync(n => n.UserId == null && n.Text.Contains("посмотреть квартиру")));
        var toCustomer = await factory.WithDbAsync(db => db.Notifications.AnyAsync(n => n.UserId == customerUserId && n.Text.Contains("ждём вас")));
        Assert.True(toManagers);
        Assert.True(toCustomer);
    }

    [Fact]
    public async Task Rest_AccessRules()
    {
        var mine = await TestScenarios.CreateDealAsync(factory);
        var foreign = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(mine.Phone);
        var url = $"/api/v1/deals/{foreign.DealId}/messages";

        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.PostAsJsonAsync(url, new SendMessageRequest("привет"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.PostAsJsonAsync($"/api/v1/deals/{mine.DealId}/messages", new SendMessageRequest(""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateContentClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateAccountantClientAsync()).GetAsync(url)).StatusCode);
    }

    private HubConnection Connect(string token)
    {
        var server = factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/chat"), options =>
            {
                // TestServer не поднимает настоящий сокет, поэтому транспорт — long polling через его обработчик.
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    private static string Token(HttpClient client) => client.DefaultRequestHeaders.Authorization!.Parameter!;

    private Task<DateTime?> ReadAtAsync(Guid messageId) =>
        factory.WithDbAsync(db => db.ChatMessages.Where(m => m.Id == messageId).Select(m => m.ReadAt).SingleAsync());
}

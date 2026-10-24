using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Client;
using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Documents;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Сводная проверка: покупатель видит только свои сделки и ничего не узнаёт о чужих.
public class CustomerIsolationTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    // Все адреса, где в пути есть dealId и которые доступны покупателю.
    private static readonly string[] DealScopedReads =
    [
        "/api/v1/deals/{0}/schedule",
        "/api/v1/deals/{0}/schedule.pdf",
        "/api/v1/deals/{0}/statement.pdf",
        "/api/v1/deals/{0}/documents",
        "/api/v1/deals/{0}/messages"
    ];

    [Fact]
    public async Task ForeignDeal_LooksExactlyLikeNonexistentOne_OnEveryDealAddress()
    {
        var (mine, foreign) = await TwoDealsWithPlansAsync();
        var customer = await factory.CreateCustomerClientAsync(mine.Phone);

        foreach (var template in DealScopedReads)
        {
            var own = await customer.GetAsync(string.Format(template, mine.DealId));
            Assert.True(own.StatusCode == HttpStatusCode.OK, $"{template} своя: {(int)own.StatusCode}");

            var foreignResponse = await customer.GetAsync(string.Format(template, foreign.DealId));
            var missingResponse = await customer.GetAsync(string.Format(template, Guid.NewGuid()));
            Assert.True(foreignResponse.StatusCode == HttpStatusCode.NotFound, $"{template} чужая: {(int)foreignResponse.StatusCode}");
            Assert.True(missingResponse.StatusCode == HttpStatusCode.NotFound, $"{template} несуществующая: {(int)missingResponse.StatusCode}");
            Assert.Equal(await DetailAsync(missingResponse), await DetailAsync(foreignResponse));
        }

        var postForeign = await customer.PostAsJsonAsync($"/api/v1/deals/{foreign.DealId}/messages", new SendMessageRequest("привет"));
        var postMissing = await customer.PostAsJsonAsync($"/api/v1/deals/{Guid.NewGuid()}/messages", new SendMessageRequest("привет"));
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (postForeign.StatusCode, postMissing.StatusCode));
        Assert.Equal(await DetailAsync(postMissing), await DetailAsync(postForeign));
    }

    [Fact]
    public async Task Customer_GetsForbidden_OnEveryStaffAddress()
    {
        var (mine, _) = await TwoDealsWithPlansAsync();
        var customer = await factory.CreateCustomerClientAsync(mine.Phone);
        var someId = Guid.NewGuid();

        (HttpMethod Method, string Url)[] staffOnly =
        [
            (HttpMethod.Get, "/api/v1/deals"),
            (HttpMethod.Get, $"/api/v1/deals/{mine.DealId}"),
            (HttpMethod.Get, $"/api/v1/deals?customerId={mine.CustomerId}"),
            (HttpMethod.Patch, $"/api/v1/deals/{mine.DealId}/stage"),
            (HttpMethod.Post, "/api/v1/deals"),
            (HttpMethod.Get, "/api/v1/customers"),
            (HttpMethod.Get, $"/api/v1/customers/{mine.CustomerId}"),
            (HttpMethod.Get, "/api/v1/leads"),
            (HttpMethod.Patch, $"/api/v1/leads/{someId}"),
            (HttpMethod.Get, "/api/v1/crm/summary"),
            (HttpMethod.Get, "/api/v1/crm/pipeline"),
            (HttpMethod.Get, "/api/v1/audit"),
            (HttpMethod.Get, "/api/v1/users"),
            (HttpMethod.Get, "/api/v1/reports/payments"),
            (HttpMethod.Get, "/api/v1/reports/debts"),
            (HttpMethod.Post, $"/api/v1/deals/{mine.DealId}/plan"),
            (HttpMethod.Post, $"/api/v1/deals/{mine.DealId}/payments"),
            (HttpMethod.Post, $"/api/v1/payments/{someId}/reverse"),
            (HttpMethod.Post, $"/api/v1/deals/{mine.DealId}/documents"),
            (HttpMethod.Post, $"/api/v1/bookings/{someId}/extend"),
            (HttpMethod.Post, $"/api/v1/bookings/{someId}/cancel"),
            (HttpMethod.Put, "/api/v1/rates"),
            (HttpMethod.Post, "/api/v1/buildings")
        ];

        foreach (var (method, url) in staffOnly)
        {
            using var request = new HttpRequestMessage(method, url);
            // Загрузка документа принимает только форму: на JSON ASP.NET ответил бы 415 ещё до проверки прав.
            if (url.EndsWith("/documents"))
                request.Content = new MultipartFormDataContent { { new ByteArrayContent("%PDF"u8.ToArray()), "file", "a.pdf" } };
            else if (method != HttpMethod.Get)
                request.Content = JsonContent.Create(new { });
            var response = await customer.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {url}: {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Customer_SeesOnlyOwnDealsAndDocuments_AndCannotGetForeignFileLink()
    {
        var (mine, foreign) = await TwoDealsWithPlansAsync();
        var manager = await factory.CreateManagerClientAsync();
        var foreignDocument = await UploadAsync(manager, foreign.DealId, "чужой договор.pdf");
        var ownDocument = await UploadAsync(manager, mine.DealId, "мой договор.pdf");
        var customer = await factory.CreateCustomerClientAsync(mine.Phone);

        var deals = await customer.GetFromJsonAsync<List<DealResponse>>("/api/v1/me/deals", TestJson.Options);
        Assert.Equal([mine.DealId], deals!.Select(d => d.Id));

        var documents = await customer.GetFromJsonAsync<List<DocumentResponse>>($"/api/v1/deals/{mine.DealId}/documents", TestJson.Options);
        Assert.Equal([ownDocument.Id], documents!.Select(d => d.Id));

        var ownLink = await customer.GetAsync($"/api/v1/documents/{ownDocument.Id}/download");
        var foreignLink = await customer.GetAsync($"/api/v1/documents/{foreignDocument.Id}/download");
        var missingLink = await customer.GetAsync($"/api/v1/documents/{Guid.NewGuid()}/download");
        Assert.Equal(HttpStatusCode.OK, ownLink.StatusCode);
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (foreignLink.StatusCode, missingLink.StatusCode));
        Assert.Equal(await DetailAsync(missingLink), await DetailAsync(foreignLink));

        // Покупатель без сделок не видит ничего.
        var stranger = await NewCustomerClientAsync();
        Assert.Empty((await stranger.GetFromJsonAsync<List<DealResponse>>("/api/v1/me/deals", TestJson.Options))!);
    }

    [Fact]
    public async Task ChatHub_DoesNotDeliverForeignDealMessages()
    {
        var (mine, foreign) = await TwoDealsWithPlansAsync();
        var otherCustomer = await factory.CreateCustomerClientAsync(foreign.Phone);
        await using var otherHub = Connect(Token(otherCustomer));
        var leaked = new List<ChatMessageResponse>();
        var ownDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        otherHub.On<ChatMessageResponse>("messageReceived", m =>
        {
            if (m.DealId == foreign.DealId) ownDelivered.TrySetResult();
            else leaked.Add(m);
        });
        await otherHub.StartAsync();
        await otherHub.InvokeAsync("JoinDeal", foreign.DealId);

        var customer = await factory.CreateCustomerClientAsync(mine.Phone);
        (await customer.PostAsJsonAsync($"/api/v1/deals/{mine.DealId}/messages", new SendMessageRequest("секрет по моей квартире")))
            .EnsureSuccessStatusCode();
        // Контрольное сообщение в его собственной сделке: когда оно пришло, чужое уже точно было бы доставлено.
        (await otherCustomer.PostAsJsonAsync($"/api/v1/deals/{foreign.DealId}/messages", new SendMessageRequest("моё")))
            .EnsureSuccessStatusCode();
        await ownDelivered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Empty(leaked);
    }

    private async Task<(DealScenario Mine, DealScenario Foreign)> TwoDealsWithPlansAsync()
    {
        var mine = await TestScenarios.CreateDealAsync(factory);
        var foreign = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, mine.DealId);
        await TestScenarios.CreatePlanAsync(factory, foreign.DealId);
        return (mine, foreign);
    }

    private async Task<HttpClient> NewCustomerClientAsync()
    {
        var phone = SitoraApiFactory.RandomPhone();
        var manager = await factory.CreateManagerClientAsync();
        (await manager.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest("Без сделок", phone, null, null, null)))
            .EnsureSuccessStatusCode();
        return await factory.CreateCustomerClientAsync(phone);
    }

    private static async Task<DocumentResponse> UploadAsync(HttpClient manager, Guid dealId, string fileName)
    {
        var file = new ByteArrayContent("%PDF-1.4 тест"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(nameof(DocumentType.Contract)), "type" } };
        return await TestScenarios.ReadAsync<DocumentResponse>(await manager.PostAsync($"/api/v1/deals/{dealId}/documents", form));
    }

    private HubConnection Connect(string token)
    {
        var server = factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    private static string Token(HttpClient client) => client.DefaultRequestHeaders.Authorization!.Parameter!;

    private static async Task<string?> DetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;
}

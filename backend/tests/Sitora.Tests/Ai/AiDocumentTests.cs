using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.DTOs.Documents;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Ai;

// Без ключа Claude проверка документов отвечает 503 и к модели не обращается.
public class AiDocumentsNotConfiguredTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Verify_WithoutClaudeKey_Returns503()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.PostAsync($"/api/v1/deals/{deal.DealId}/documents/verify", AiDocumentTests.Png("pasport.png"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(ClaudeDocumentVerifier.NotConfigured, await response.Content.ReadAsStringAsync());
    }
}

// Модель подменена: проверяется всё вокруг неё — доступ, аудит, подбор пункта графика, контекст чата.
public class AiDocumentFactory : SitoraApiFactory
{
    public FakeDocumentVerifier Verifier { get; } = new();
    public FakeChatModel Model { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Claude:ApiKey", "test-key-not-real");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IDocumentVerifier>(Verifier);
            services.AddSingleton<IChatModel>(Model);
        });
    }
}

public class AiDocumentTests(AiDocumentFactory factory) : IClassFixture<AiDocumentFactory>
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    [Fact]
    public async Task VerifyStored_UsesBuyerName_AndWritesAudit()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var manager = await factory.CreateManagerClientAsync();
        var form = Png("pasport.png");
        form.Add(new StringContent("Other"), "type");
        var document = await TestScenarios.ReadAsync<DocumentResponse>(await manager.PostAsync($"/api/v1/deals/{deal.DealId}/documents", form));
        factory.Verifier.Passport = new DocumentVerifyResult(true, "Покупатель Кабинета", new DateOnly(2030, 1, 1), true, false, null, "{}");

        var result = await TestScenarios.ReadAsync<DocumentVerifyResult>(await manager.PostAsync($"/api/v1/documents/{document.Id}/verify", null));

        Assert.True(result.IsValid);
        Assert.Equal("Покупатель Кабинета", factory.Verifier.LastExpectedName);
        Assert.Equal("image/png", factory.Verifier.LastContentType);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Entity == "Document" && l.EntityId == document.Id && l.Action == "AiVerified")));

        // Бухгалтер документы на ИИ не проверяет; чужой документ — 404.
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateAccountantClientAsync()).PostAsync($"/api/v1/documents/{document.Id}/verify", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsync($"/api/v1/documents/{Guid.NewGuid()}/verify", null)).StatusCode);
    }

    [Fact]
    public async Task ReadReceipt_AmountWithinOnePercent_SuggestsScheduleItem_OtherwiseNone()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var schedule = await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var item = schedule.Items.Where(i => i.AmountPaid < i.AmountDue).OrderBy(i => i.DueDate).First();
        var left = item.AmountDue - item.AmountPaid;
        var accountant = await factory.CreateAccountantClientAsync();

        factory.Verifier.Receipt = new ReceiptReadResult(true, Math.Round(left * 1.005m, 2), "TJS", item.DueDate.AddDays(3), "Оплата по договору", "Амонатбонк", "{}");
        var matched = await TestScenarios.ReadAsync<ReceiptReadResponse>(await accountant.PostAsync($"/api/v1/deals/{deal.DealId}/payments/read-receipt", Png("chek.png")));
        Assert.Equal(item.Id, matched.SuggestedScheduleItemId);
        Assert.Equal((item.DueDate, left, "TJS"), (matched.SuggestedDueDate!.Value, matched.SuggestedAmount!.Value, matched.SuggestedCurrency));

        factory.Verifier.Receipt = new ReceiptReadResult(true, Math.Round(left * 1.5m, 2), "TJS", item.DueDate, null, null, "{}");
        var unmatched = await TestScenarios.ReadAsync<ReceiptReadResponse>(await accountant.PostAsync($"/api/v1/deals/{deal.DealId}/payments/read-receipt", Png("chek.png")));
        Assert.Null(unmatched.SuggestedScheduleItemId);

        // Та же сумма, но в другой валюте — не сравниваем.
        factory.Verifier.Receipt = new ReceiptReadResult(true, left, "USD", item.DueDate, null, null, "{}");
        var otherCurrency = await TestScenarios.ReadAsync<ReceiptReadResponse>(await accountant.PostAsync($"/api/v1/deals/{deal.DealId}/payments/read-receipt", Png("chek.png")));
        Assert.Null(otherCurrency.SuggestedScheduleItemId);
    }

    [Fact]
    public async Task ChatWithDeal_PutsDealIntoStaffPrompt_ButNotForBuyer()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var manager = await factory.CreateManagerClientAsync();

        await TestScenarios.ReadAsync<AiChatResponse>(await manager.PostAsJsonAsync($"/api/v1/ai/chat?dealId={deal.DealId}",
            new AiChatRequest("Напомни клиенту о платеже", null)));

        var system = factory.Model.LastSystem!;
        Assert.StartsWith("Текущая сделка: Покупатель Кабинета, квартира ", system);
        Assert.Contains("статус договор", system);
        Assert.Contains("Платежей: 0/", system);
        Assert.EndsWith(AiAssistant.StaffPrompt, system);

        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        await TestScenarios.ReadAsync<AiChatResponse>(await customer.PostAsJsonAsync($"/api/v1/ai/chat?dealId={deal.DealId}",
            new AiChatRequest("Когда платить?", null)));
        Assert.Equal(AiAssistant.CustomerPrompt, factory.Model.LastSystem);

        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"/api/v1/ai/chat?dealId={Guid.NewGuid()}",
            new AiChatRequest("Кто это?", null))).StatusCode);
    }

    [Fact]
    public void Parser_ReadsFencedJson_ChecksExpiryItself_AndFlagsGarbage()
    {
        var today = new DateOnly(2026, 10, 31);
        var ok = ClaudeDocumentVerifier.ParsePassport("```json\n{\"extractedFullName\":\"Алиев Фарход\",\"expiryDate\":\"2031-05-01\",\"nameMatches\":true,\"isExpired\":false,\"isReadable\":true,\"issue\":null}\n```", today);
        Assert.Equal((true, "Алиев Фарход", new DateOnly(2031, 5, 1), false), (ok.IsValid, ok.ExtractedFullName, ok.ExpiryDate, ok.IsExpired));

        // Модель сказала «не просрочен», но дата прошла — решает дата.
        var expired = ClaudeDocumentVerifier.ParsePassport("{\"extractedFullName\":\"Алиев\",\"expiryDate\":\"2025-01-01\",\"nameMatches\":true,\"isExpired\":false,\"isReadable\":true}", today);
        Assert.Equal((false, true, "Срок действия истёк"), (expired.IsValid, expired.IsExpired, expired.Issue));

        var mismatch = ClaudeDocumentVerifier.ParsePassport("{\"extractedFullName\":\"Другой Человек\",\"expiryDate\":\"2031-01-01\",\"nameMatches\":false,\"isReadable\":true}", today);
        Assert.Equal((false, false, "ФИО не совпадает"), (mismatch.IsValid, mismatch.NameMatches, mismatch.Issue));

        var garbage = ClaudeDocumentVerifier.ParsePassport("Извините, не вижу документа", today);
        Assert.Equal((false, ClaudeDocumentVerifier.Unreadable), (garbage.IsValid, garbage.Issue));

        var receipt = ClaudeDocumentVerifier.ParseReceipt("{\"amount\":\"12 500,50\",\"currency\":\"tjs\",\"paymentDate\":\"2026-10-20\",\"bankName\":\"Алиф\",\"isReadable\":true}");
        Assert.Equal((true, 12500.50m, "TJS", new DateOnly(2026, 10, 20)), (receipt.IsReadable, receipt.Amount!.Value, receipt.Currency, receipt.PaymentDate!.Value));
        Assert.False(ClaudeDocumentVerifier.ParseReceipt("{\"amount\":0,\"isReadable\":true}").IsReadable);
    }

    public static MultipartFormDataContent Png(string name)
    {
        var file = new ByteArrayContent(PngBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", name } };
    }
}

public class FakeDocumentVerifier : IDocumentVerifier
{
    private readonly ConcurrentQueue<string> _names = new();
    public DocumentVerifyResult Passport { get; set; } = new(true, null, null, true, false, null, "{}");
    public ReceiptReadResult Receipt { get; set; } = new(false, null, null, null, null, null, "{}");
    public string? LastExpectedName => _names.LastOrDefault();
    public string? LastContentType { get; private set; }

    public Task<DocumentVerifyResult> VerifyPassportAsync(Stream imageStream, string contentType, string expectedFullName, CancellationToken ct = default)
    {
        _names.Enqueue(expectedFullName);
        LastContentType = contentType;
        return Task.FromResult(Passport);
    }

    public Task<ReceiptReadResult> ReadReceiptAsync(Stream imageStream, string contentType, CancellationToken ct = default) =>
        Task.FromResult(Receipt);
}

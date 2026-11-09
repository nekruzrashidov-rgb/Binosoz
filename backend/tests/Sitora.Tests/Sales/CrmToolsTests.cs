using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

public class CrmToolsTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task ImportClients_PreviewWritesNothing_ThenAddsNewAndSkipsDuplicates()
    {
        var admin = await factory.CreateAdminClientAsync();
        var existing = SitoraApiFactory.RandomPhone();
        (await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest("Уже есть", existing, null, null, null))).EnsureSuccessStatusCode();
        var fresh = SitoraApiFactory.RandomPhone();
        var second = SitoraApiFactory.RandomPhone();

        var rows = new[]
        {
            new[] { "Новый Клиент", fresh, "New@Mail.tj", "звонил вчера" },
            ["Повтор в файле", fresh, "", ""],
            ["Есть в базе", existing, "", ""],
            ["Без телефона", "12", "", ""],
            ["Второй", second, "", ""]
        };

        var preview = await TestScenarios.ReadAsync<ClientImportResult>(await admin.PostAsync("/api/v1/crm/clients/import?dryRun=true", Excel(rows)));
        Assert.True(preview.DryRun);
        Assert.Equal(["new", "duplicate", "duplicate", "invalid", "new"], preview.Rows.Select(r => r.Status));
        Assert.Equal((2, 2, 1), (preview.Added, preview.Skipped, preview.Invalid));
        Assert.Equal("new@mail.tj", preview.Rows[0].Email);
        Assert.False(await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Phone == PhoneNumber.Normalize(fresh))));

        var done = await TestScenarios.ReadAsync<ClientImportResult>(await admin.PostAsync("/api/v1/crm/clients/import", Excel(rows)));
        Assert.Equal(2, done.Added);
        var names = await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters()
            .Where(c => c.Phone == PhoneNumber.Normalize(fresh) || c.Phone == PhoneNumber.Normalize(second))
            .OrderBy(c => c.FullName).Select(c => c.FullName).ToListAsync());
        Assert.Equal(["Второй", "Новый Клиент"], names);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.Entity == "Customer" && a.Action == "Imported")));

        // Тот же файл ещё раз: все уже есть.
        var again = await TestScenarios.ReadAsync<ClientImportResult>(await admin.PostAsync("/api/v1/crm/clients/import", Excel(rows)));
        Assert.Equal(0, again.Added);

        using var csv = new MultipartFormDataContent { { new StringContent("Имя;Телефон"), "file", "clients.csv" } };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsync("/api/v1/crm/clients/import", csv)).StatusCode);
        var accountant = await factory.CreateAccountantClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsync("/api/v1/crm/clients/import", Excel(rows))).StatusCode);
    }

    [Fact]
    public async Task Templates_Crud_WithValidationAndUniqueName()
    {
        var manager = await factory.CreateManagerClientAsync();
        var name = "Напоминание " + Guid.NewGuid().ToString("N")[..6];

        var created = await TestScenarios.ReadAsync<MessageTemplateResponse>(await manager.PostAsJsonAsync("/api/v1/crm/templates",
            new MessageTemplateRequest(name, "Здравствуйте, {name}! Платёж {amount} по квартире {apt}.", "Payment")));
        Assert.Equal("payment", created.Type);
        Assert.Contains((await manager.GetFromJsonAsync<List<MessageTemplateResponse>>("/api/v1/crm/templates", TestJson.Options))!, t => t.Id == created.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync("/api/v1/crm/templates",
            new MessageTemplateRequest(name, "Другой текст", "welcome"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await manager.PostAsJsonAsync("/api/v1/crm/templates",
            new MessageTemplateRequest("Шаблон", "Текст", "sms"))).StatusCode);

        var updated = await TestScenarios.ReadAsync<MessageTemplateResponse>(await manager.PutAsJsonAsync($"/api/v1/crm/templates/{created.Id}",
            new MessageTemplateRequest(name, "Новый текст для {name}", "booking")));
        Assert.Equal(("booking", "Новый текст для {name}"), (updated.Type, updated.Text));

        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/api/v1/crm/templates/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.DeleteAsync($"/api/v1/crm/templates/{created.Id}")).StatusCode);
        Assert.Equal(3, await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.EntityId == created.Id)));
    }

    [Fact]
    public async Task SalesAnalytics_Has12MonthsAndFunnel_ProjectCardOnlyForLeaders()
    {
        await TestScenarios.CreateDealAsync(factory);
        var manager = await factory.CreateManagerClientAsync();
        var analytics = (await manager.GetFromJsonAsync<SalesAnalyticsResponse>("/api/v1/crm/analytics/sales", TestJson.Options))!;
        Assert.Equal(12, analytics.Months.Count);
        Assert.Equal(DateTime.UtcNow.AddHours(5).ToString("yyyy-MM"), analytics.Months[^1].Month);
        Assert.True(analytics.Months[^1].Deals >= 1);
        Assert.Equal(["leads", "bookings", "deals", "completed"], analytics.Funnel.Select(f => f.Key));
        Assert.Contains(analytics.Funnel, f => f.Percent == 100m);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/crm/analytics/project")).StatusCode);
        var admin = await factory.CreateAdminClientAsync();
        var cards = (await admin.GetFromJsonAsync<List<ProjectCardResponse>>("/api/v1/crm/analytics/project", TestJson.Options))!;
        var card = Assert.Single(cards.Where(c => c.TotalApartments > 0).Take(1));
        Assert.Equal(card.TotalApartments, card.Sold + card.Booked + card.Free);
        Assert.True(card.Plan > 0);
        Assert.InRange(card.ConstructionPercent, 0, 100);
    }

    private static MultipartFormDataContent Excel(string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Клиенты");
        string[] headers = ["Имя", "Телефон", "Email", "Комментарий"];
        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(1, col + 1).Value = headers[col];
        for (var row = 0; row < rows.Length; row++)
        for (var col = 0; col < rows[row].Length; col++)
            sheet.Cell(row + 2, col + 1).Value = rows[row][col];
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { file, "file", "clients.xlsx" } };
    }
}

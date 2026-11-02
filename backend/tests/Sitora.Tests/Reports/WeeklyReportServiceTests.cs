using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Cash;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Construction;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Reports;

public class WeeklyReportServiceTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    // Последняя завершённая неделя пн–вс по Душанбе — за неё формируется отчёт.
    private static (DateOnly Start, DateOnly End) LastWeek
    {
        get
        {
            var today = TestScenarios.Today;
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            return (monday.AddDays(-7), monday.AddDays(-1));
        }
    }

    [Fact]
    public async Task Generate_ByOwner_StoresExcelAndPdf_WritesAudit_AndRegenerationReplaces()
    {
        var owner = await factory.CreateAdminClientAsync();
        var building = await NewBuildingAsync(owner, "Дом для отчёта");

        var report = await GenerateAsync(owner, building);
        Assert.Equal((LastWeek.Start, LastWeek.End, true), (report.PeriodStart, report.PeriodEnd, report.IsManual));

        using var workbook = await ExcelAsync(owner, building, report.Id);
        Assert.Equal(["Сводка", "Платежи", "Касса", "Просрочки"], workbook.Worksheets.Select(w => w.Name));
        var pdf = await owner.GetAsync($"/api/v1/buildings/{building}/reports/weekly/{report.Id}/pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Entity == "WeeklyReport" && l.EntityId == report.Id && l.Action == "Generate")));

        // Повторная генерация за ту же неделю перезаписывает отчёт, а не добавляет второй.
        var again = await GenerateAsync(owner, building);
        Assert.Equal(report.Id, again.Id);
        var list = await owner.GetFromJsonAsync<List<WeeklyReportResponse>>($"/api/v1/buildings/{building}/reports/weekly", TestJson.Options);
        Assert.Equal([report.Id], list!.Select(r => r.Id));
    }

    [Fact]
    public async Task Summary_CountsCashOfTheWeekOnly_ProfitAndConstruction()
    {
        var owner = await factory.CreateAdminClientAsync();
        var accountant = await factory.CreateAccountantClientAsync();
        var building = await NewBuildingAsync(owner, "Дом со сводкой");
        var (start, end) = LastWeek;
        await AddCashAsync(accountant, building, 5000m, CashDirection.Income, CashCategory.Rent, start);
        await AddCashAsync(accountant, building, 1200m, CashDirection.Expense, CashCategory.Salaries, end);
        await AddCashAsync(accountant, building, 999m, CashDirection.Income, CashCategory.OtherIncome, start.AddDays(-1)); // до недели — не входит
        foreach (var (name, percent, order) in new[] { ("Фундамент", 100, 1), ("Каркас", 40, 2), ("Фасад", 10, 3) })
            await TestScenarios.ReadAsync<object>(await owner.PostAsJsonAsync($"/api/v1/buildings/{building}/stages", new SaveStageRequest(name, percent, order)));

        var report = await GenerateAsync(owner, building);
        using var workbook = await ExcelAsync(owner, building, report.Id);

        var summary = workbook.Worksheet("Сводка");
        Assert.Equal(5000m, Value(summary, "Поступления по кассе, TJS"));
        Assert.Equal(1200m, Value(summary, "Расходы по кассе, TJS"));
        Assert.Equal(3800m, Value(summary, "Чистая прибыль, TJS"));
        Assert.Equal(50m, Value(summary, "Готовность стройки, %"));
        Assert.Equal(0m, Value(summary, "Новых сделок"));
        Assert.Equal(2, workbook.Worksheet("Касса").RowsUsed().Count() - 1);
    }

    [Fact]
    public async Task EmptyWeek_HasZeroTotals_AndSheetsSayNoRecords()
    {
        var owner = await factory.CreateAdminClientAsync();
        var building = await NewBuildingAsync(owner, "Пустой дом");

        var report = await GenerateAsync(owner, building);
        using var workbook = await ExcelAsync(owner, building, report.Id);

        var summary = workbook.Worksheet("Сводка");
        Assert.Equal((0m, 0m, 0m, 0m), (Value(summary, "Поступления по кассе, TJS"), Value(summary, "Расходы по кассе, TJS"),
            Value(summary, "Чистая прибыль, TJS"), Value(summary, "Готовность стройки, %")));
        foreach (var name in new[] { "Платежи", "Касса", "Просрочки" })
            Assert.Equal("За неделю записей нет", workbook.Worksheet(name).Cell(2, 1).GetString());
    }

    [Fact]
    public async Task PaymentOfTheWeek_NewDeal_AndOverdue_AppearInReport()
    {
        var owner = await factory.CreateAdminClientAsync();
        var accountant = await factory.CreateAccountantClientAsync();
        var deal = await TestScenarios.CreateDealAsync(factory);
        // График начался два месяца назад: первые пункты к понедельнику уже просрочены.
        await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: TestScenarios.Today.AddMonths(-2));
        Assert.True((await TestScenarios.PayAsync(accountant, deal.DealId, 1234m, paidAt: LastWeek.Start)).IsSuccessStatusCode);
        var (building, apartment) = await factory.WithDbAsync(db => db.Deals.Where(d => d.Id == deal.DealId)
            .Select(d => new ValueTuple<Guid, int>(d.Apartment.Floor.BuildingId, d.Apartment.Number)).SingleAsync());

        var report = await GenerateAsync(owner, building);
        using var workbook = await ExcelAsync(owner, building, report.Id);

        var payments = workbook.Worksheet("Платежи");
        Assert.Contains(payments.RowsUsed().Skip(1), r => r.Cell(2).GetString() == "Покупатель Кабинета"
                                                          && r.Cell(3).GetValue<int>() == apartment && r.Cell(4).GetValue<decimal>() == 1234m);
        var overdue = workbook.Worksheet("Просрочки");
        Assert.Contains(overdue.RowsUsed().Skip(1), r => r.Cell(2).GetValue<int>() == apartment && r.Cell(6).GetValue<int>() > 0);
        Assert.True(Value(workbook.Worksheet("Сводка"), $"Просроченных платежей на {LastWeek.End.AddDays(1):dd.MM.yyyy}") > 0);
    }

    [Fact]
    public async Task Isolation_AndRoles_ForeignBuildingIs404_OnlyOwnerGenerates()
    {
        var owner = await factory.CreateAdminClientAsync();
        var accountant = await factory.CreateAccountantClientAsync();
        var manager = await factory.CreateManagerClientAsync();
        var first = await NewBuildingAsync(owner, "Дом А для изоляции");
        var second = await NewBuildingAsync(owner, "Дом Б для изоляции");
        var report = await GenerateAsync(owner, first);

        // Отчёт дома А по адресу дома Б и несуществующий дом — 404.
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/buildings/{second}/reports/weekly/{report.Id}/excel")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/buildings/{second}/reports/weekly/{report.Id}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/buildings/{Guid.NewGuid()}/reports/weekly")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/v1/buildings/{Guid.NewGuid()}/reports/weekly/generate", null)).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<List<WeeklyReportResponse>>($"/api/v1/buildings/{second}/reports/weekly", TestJson.Options))!);

        // Бухгалтер смотрит и скачивает, но не формирует; менеджеру раздел закрыт; без входа — 401.
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync($"/api/v1/buildings/{first}/reports/weekly")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync($"/api/v1/buildings/{first}/reports/weekly/{report.Id}/excel")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsync($"/api/v1/buildings/{first}/reports/weekly/generate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/v1/buildings/{first}/reports/weekly")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/v1/buildings/{first}/reports/weekly")).StatusCode);
    }

    [Fact]
    public async Task Job_GeneratesForBuildingsOfOrganizationsWithOwner_OnceAWeek()
    {
        var owner = await factory.CreateAdminClientAsync();
        var building = await NewBuildingAsync(owner, "Дом для фоновой задачи");

        Assert.True(await RunJobAsync() > 0);
        var report = Assert.Single((await owner.GetFromJsonAsync<List<WeeklyReportResponse>>($"/api/v1/buildings/{building}/reports/weekly", TestJson.Options))!);
        Assert.False(report.IsManual);
        // Второй запуск за ту же неделю ничего не добавляет.
        Assert.Equal(0, await RunJobAsync());
    }

    private async Task<int> RunJobAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IWeeklyReportService>().GenerateForAllAsync();
    }

    private static async Task<Guid> NewBuildingAsync(HttpClient owner, string name) =>
        (await TestScenarios.ReadAsync<BuildingResponse>(await owner.PostAsJsonAsync("/api/v1/buildings",
            new SaveBuildingRequest($"{name} {Guid.NewGuid():N}"[..40], name, "г. Душанбе", 3, null, null)))).Id;

    private static async Task<WeeklyReportResponse> GenerateAsync(HttpClient owner, Guid building)
    {
        var response = await owner.PostAsync($"/api/v1/buildings/{building}/reports/weekly/generate", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WeeklyReportResponse>(TestJson.Options))!;
    }

    private static async Task<XLWorkbook> ExcelAsync(HttpClient client, Guid building, Guid report)
    {
        var response = await client.GetAsync($"/api/v1/buildings/{building}/reports/weekly/{report}/excel");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    private static decimal Value(IXLWorksheet sheet, string title) =>
        sheet.Column(1).CellsUsed().Single(c => c.GetString() == title).CellRight().GetValue<decimal>();

    private static async Task AddCashAsync(HttpClient client, Guid building, decimal amount, CashDirection direction, CashCategory category, DateOnly date) =>
        await TestScenarios.ReadAsync<CashTransactionResponse>(await client.PostAsJsonAsync($"/api/v1/buildings/{building}/cash",
            new CreateCashTransactionRequest(amount, Currency.TJS, direction, category, "Операция для отчёта", date, null), TestJson.Options));
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Documents;
using Sitora.Infrastructure.Integrations;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

// Число страниц PDF — из словаря /Type /Pages … /Count; не нашли — null (такой файл не считается).
public class PdfPagesTests
{
    private static byte[] Pdf(string body) => Encoding.Latin1.GetBytes("%PDF-1.4\n" + body + "\n%%EOF");

    [Fact]
    public void RootCount_InAnyKeyOrder_LargestWins()
    {
        Assert.Equal(3, PdfPages.Count(Pdf("1 0 obj << /Type /Pages /Kids [2 0 R 3 0 R] /Count 3 >> endobj")));
        Assert.Equal(7, PdfPages.Count(Pdf("1 0 obj << /Count 7 /Kids [4 0 R] /Type /Pages >> endobj 4 0 obj << /Type /Pages /Count 2 >> endobj")));
    }

    [Fact]
    public void Unknown_IsNull()
    {
        Assert.Null(PdfPages.Count(Pdf("1 0 obj << /Type /ObjStm /N 5 >> stream x endstream endobj")));
        Assert.Null(PdfPages.Count(Encoding.ASCII.GetBytes("not a pdf")));
        Assert.Null(PdfPages.Count(null));
    }
}

// «Зелёный эффект»: только реальные счётчики (документы, страницы из PDF, действия онлайн); CO₂ — только с коэффициентом.
public class GreenImpactTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task IssuedAndUploadedPdfs_AreCounted_Co2OnlyWithFactor()
    {
        var guest = factory.CreateClient();
        var before = await guest.GetFromJsonAsync<JsonElement>("/api/v1/public/green-impact?organization=nekruz&v=1");
        Assert.False(before.TryGetProperty("co2KgSaved", out _));

        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var manager = await factory.CreateManagerClientAsync();

        // Выданный онлайн график платежей (QuestPDF): записан вместе с числом страниц из самого файла.
        var schedule = await manager.GetAsync($"/api/v1/deals/{deal.DealId}/schedule.pdf");
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        var expectedPages = PdfPages.Count(await schedule.Content.ReadAsByteArrayAsync());
        Assert.True(expectedPages >= 1);
        var issued = await factory.WithDbAsync(db => db.IssuedPdfs.SingleAsync(x => x.DealId == deal.DealId));
        Assert.Equal(expectedPages, issued.PageCount);

        // Загруженный PDF: страницы считаются при загрузке.
        var file = new ByteArrayContent(Encoding.Latin1.GetBytes("%PDF-1.4\n1 0 obj << /Type /Pages /Kids [] /Count 4 >> endobj\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent { { file, "file", "dogovor.pdf" }, { new StringContent("Contract"), "type" } };
        var uploaded = await manager.PostAsync($"/api/v1/deals/{deal.DealId}/documents", form);
        Assert.True(uploaded.IsSuccessStatusCode, await uploaded.Content.ReadAsStringAsync());
        var doc = await uploaded.Content.ReadFromJsonAsync<DocumentResponse>(TestJson.Options);
        Assert.Equal(4, await factory.WithDbAsync(db => db.Documents.Where(d => d.Id == doc!.Id).Select(d => d.PageCount).SingleAsync()));

        var admin = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/owner/green-settings",
            new { co2KgPerKgPaper = 1.1m, co2Source = "Тестовый источник CO₂" })).StatusCode);

        var stats = await manager.GetFromJsonAsync<JsonElement>("/api/v1/stats/green");
        var totals = stats.GetProperty("totals");
        var pages = totals.GetProperty("pagesSaved").GetInt32();
        Assert.True(pages >= expectedPages!.Value + 4);
        Assert.Equal(Math.Round(pages * 0.005m, 2, MidpointRounding.AwayFromZero), totals.GetProperty("paperKgSaved").GetDecimal());
        Assert.Equal(Math.Round(pages * 0.005m * 1.1m, 2, MidpointRounding.AwayFromZero), totals.GetProperty("co2KgSaved").GetDecimal());
        Assert.Equal("Тестовый источник CO₂", totals.GetProperty("co2Source").GetString());
        Assert.Equal(12, stats.GetProperty("months").GetArrayLength());
        Assert.True(stats.GetProperty("months")[11].GetProperty("pages").GetInt32() >= expectedPages.Value + 4);

        // Публичный агрегат — те же цифры (без кэша прежнего ответа: другой адрес).
        var pub = await guest.GetFromJsonAsync<JsonElement>("/api/v1/public/green-impact?organization=nekruz&v=2");
        Assert.Equal(pages, pub.GetProperty("pagesSaved").GetInt32());
        Assert.True(pub.TryGetProperty("co2KgSaved", out _));
    }

    [Fact]
    public async Task Access_AndOrganizationRequired()
    {
        var guest = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.GetAsync("/api/v1/public/green-impact")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/v1/public/green-impact?organization=no-such-org")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/v1/stats/green")).StatusCode);
        var content = await factory.CreateContentClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await content.GetAsync("/api/v1/stats/green")).StatusCode);
    }
}

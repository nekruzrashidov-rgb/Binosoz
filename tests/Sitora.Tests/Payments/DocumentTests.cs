using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Documents;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

public class DocumentTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n% тестовый договор\n%%EOF");

    [Fact]
    public async Task ManagerUploads_CustomerSeesAndDownloads_OtherCustomerGets404()
    {
        var mine = await TestScenarios.CreateDealAsync(factory);
        var foreign = await TestScenarios.CreateDealAsync(factory);
        var manager = await factory.CreateManagerClientAsync();

        var upload = await manager.PostAsync($"/api/v1/deals/{mine.DealId}/documents", Form(PdfBytes, "Договор №1.pdf", DocumentType.Contract));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var document = await TestScenarios.ReadAsync<DocumentResponse>(upload);
        Assert.Equal(("Договор №1.pdf", "application/pdf", DocumentType.Contract), (document.FileName, document.ContentType, document.Type));

        var customer = await factory.CreateCustomerClientAsync(mine.Phone);
        var list = await customer.GetFromJsonAsync<List<DocumentResponse>>($"/api/v1/deals/{mine.DealId}/documents", TestJson.Options);
        Assert.Equal(document.Id, Assert.Single(list!).Id);

        var download = await customer.GetAsync($"/api/v1/documents/{document.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(PdfBytes, await download.Content.ReadAsByteArrayAsync());

        var other = await factory.CreateCustomerClientAsync(foreign.Phone);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/documents/{document.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/deals/{mine.DealId}/documents")).StatusCode);

        var audit = await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.EntityId == document.Id && a.Action == "Uploaded"));
        Assert.True(audit);
    }

    [Fact]
    public async Task Upload_RejectsWrongContent_AndWrongRoles()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var manager = await factory.CreateManagerClientAsync();
        var url = $"/api/v1/deals/{deal.DealId}/documents";

        var exeAsPdf = await manager.PostAsync(url, Form("MZ\x90\0"u8.ToArray(), "договор.pdf", DocumentType.Contract));
        var wrongExtension = await manager.PostAsync(url, Form(PdfBytes, "script.exe", DocumentType.Other));
        var content = await (await factory.CreateContentClientAsync()).PostAsync(url, Form(PdfBytes, "a.pdf", DocumentType.Act));
        var customer = await (await factory.CreateCustomerClientAsync(deal.Phone)).PostAsync(url, Form(PdfBytes, "a.pdf", DocumentType.Act));
        var accountant = await (await factory.CreateAccountantClientAsync()).PostAsync(url, Form(PdfBytes, "акт.pdf", DocumentType.Act));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exeAsPdf.StatusCode);
        Assert.Contains("не соответствует", await exeAsPdf.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongExtension.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, content.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        Assert.Equal(HttpStatusCode.Created, accountant.StatusCode);
    }

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, DocumentType type)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent
        {
            { file, "file", fileName },
            { new StringContent(type.ToString()), "type" }
        };
    }
}

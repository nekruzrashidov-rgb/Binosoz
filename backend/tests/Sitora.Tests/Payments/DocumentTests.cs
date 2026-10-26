using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Documents;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Security;
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

        // Вместо файла — подписанная ссылка на 15 минут; скачать по ней можно без входа.
        var link = await TestScenarios.ReadAsync<FileLinkResponse>(await customer.GetAsync($"/api/v1/documents/{document.Id}/download"));
        Assert.StartsWith("/api/v1/files?token=", link.Url);
        Assert.InRange(link.ExpiresAt - DateTime.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.1));

        var download = await factory.CreateClient().GetAsync(link.Url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(PdfBytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("Договор №1.pdf", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Contains("no-store", download.Headers.CacheControl?.ToString());

        var other = await factory.CreateCustomerClientAsync(foreign.Phone);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/documents/{document.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/deals/{mine.DealId}/documents")).StatusCode);

        var audit = await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.EntityId == document.Id && a.Action == "Uploaded"));
        Assert.True(audit);
    }

    [Fact]
    public async Task FileLinks_RejectForgedExpiredAndWrongKindTokens()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var manager = await factory.CreateManagerClientAsync();
        var document = await TestScenarios.ReadAsync<DocumentResponse>(
            await manager.PostAsync($"/api/v1/deals/{deal.DealId}/documents", Form(PdfBytes, "акт.pdf", DocumentType.Act)));
        var link = await TestScenarios.ReadAsync<FileLinkResponse>(await manager.GetAsync($"/api/v1/documents/{document.Id}/download"));
        var token = link.Url["/api/v1/files?token=".Length..];
        var guest = factory.CreateClient();

        string expired = "", asPhoto = "", foreignKey = "";
        await factory.WithServiceAsync<FileLinkSigner>(signer =>
        {
            expired = signer.CreateToken(FileLinkKind.Document, document.Id, DateTime.UtcNow.AddSeconds(-1));
            // Подпись от ссылки на документ не подходит к фото с тем же id.
            asPhoto = signer.CreateToken(FileLinkKind.Photo, document.Id, DateTime.UtcNow.AddMinutes(5));
            return Task.CompletedTask;
        });
        var otherSigner = new FileLinkSigner(Options.Create(new JwtOptions { Key = new string('x', 48) }), TimeProvider.System);
        foreignKey = otherSigner.CreateToken(FileLinkKind.Document, document.Id, DateTime.UtcNow.AddMinutes(5));
        var tampered = token[..^2] + (token[^2] == 'A' ? "BB" : "AA");

        foreach (var bad in new[] { expired, asPhoto, foreignKey, tampered, "мусор", "" })
        {
            var response = await guest.GetAsync("/api/v1/files?token=" + Uri.EscapeDataString(bad));
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{bad}: {(int)response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(link.Url)).StatusCode);
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

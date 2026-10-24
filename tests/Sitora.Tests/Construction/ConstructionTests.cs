using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Construction;
using Sitora.Domain.DTOs.Sales;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Construction;

public class ConstructionTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    [Fact]
    public async Task CustomerWithDeal_SeesStagePercentsAndOverallProgress()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);

        var progress = await customer.GetFromJsonAsync<BuildingProgressResponse>(
            $"/api/v1/buildings/{await SeededBuildingIdAsync()}/stages", TestJson.Options);

        Assert.True(progress!.Stages.Count >= ConstructionSeeder.Stages.Length);
        Assert.Equal(65, progress.Stages.Single(s => s.Name == "Монолитный каркас").Percent);
        Assert.Equal(100, progress.Stages.Single(s => s.Name == "Котлован").Percent);
        Assert.Equal((int)Math.Round(progress.Stages.Average(s => s.Percent), MidpointRounding.AwayFromZero), progress.OverallPercent);
        Assert.Equal(progress.Stages.OrderBy(s => s.SortOrder).Select(s => s.Id), progress.Stages.Select(s => s.Id));
    }

    [Fact]
    public async Task Progress_HiddenFromCustomersWithoutDealInBuilding()
    {
        var url = $"/api/v1/buildings/{await SeededBuildingIdAsync()}/stages";
        var manager = await factory.CreateManagerClientAsync();
        var phone = SitoraApiFactory.RandomPhone();
        (await manager.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest("Без сделки", phone, null, null, null))).EnsureSuccessStatusCode();
        var customerWithoutDeal = await factory.CreateCustomerClientAsync(phone);

        Assert.Equal(HttpStatusCode.NotFound, (await customerWithoutDeal.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateContentClientAsync()).GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Content_ManagesStagesAndPhotos_CustomerSeesThem()
    {
        var buildingId = await SeededBuildingIdAsync();
        var content = await factory.CreateContentClientAsync();

        var stage = await TestScenarios.ReadAsync<StageResponse>(await content.PostAsJsonAsync(
            $"/api/v1/buildings/{buildingId}/stages", new SaveStageRequest("Благоустройство", 0, 900)));
        var updated = await TestScenarios.ReadAsync<StageResponse>(await content.PutAsJsonAsync(
            $"/api/v1/stages/{stage.Id}", new SaveStageRequest("Благоустройство", 30, 900)));
        Assert.Equal(30, updated.Percent);

        var audit = await factory.WithDbAsync(db => db.AuditLogs.SingleAsync(a => a.EntityId == stage.Id && a.Action == "Updated"));
        Assert.Contains("\"Percent\":0", audit.Before!.Replace(" ", ""));
        Assert.Contains("\"Percent\":30", audit.After!.Replace(" ", ""));

        var photo = await TestScenarios.ReadAsync<PhotoResponse>(await content.PostAsync(
            $"/api/v1/stages/{stage.Id}/photos", PhotoForm(PngBytes, "двор.png", "Детская площадка")));
        Assert.StartsWith("/api/v1/files?token=", photo.Url);

        var deal = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var progress = await customer.GetFromJsonAsync<BuildingProgressResponse>($"/api/v1/buildings/{buildingId}/stages", TestJson.Options);
        var seen = Assert.Single(progress!.Stages.Single(s => s.Id == stage.Id).Photos);
        Assert.Equal("Детская площадка", seen.Caption);

        // Ссылку из ответа можно поставить прямо в <img src>: браузер откроет её без JWT, картинкой, а не скачиванием.
        var image = await factory.CreateClient().GetAsync(seen.Url);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Null(image.Content.Headers.ContentDisposition);
        Assert.Equal(PngBytes, await image.Content.ReadAsByteArrayAsync());

        // Старого адреса фото под JWT больше нет — файлы отдаются только по подписанным ссылкам.
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/v1/construction/photos/{photo.Id}")).StatusCode);
    }

    [Fact]
    public async Task StageChanges_ValidatedAndOnlyForContentOrAdmin()
    {
        var buildingId = await SeededBuildingIdAsync();
        var stageId = await factory.WithDbAsync(db => db.ConstructionStages.Where(s => s.BuildingId == buildingId).Select(s => s.Id).FirstAsync());
        var content = await factory.CreateContentClientAsync();

        var manager = await (await factory.CreateManagerClientAsync()).PutAsJsonAsync($"/api/v1/stages/{stageId}", new SaveStageRequest("Котлован", 100, 10));
        var tooMuch = await content.PutAsJsonAsync($"/api/v1/stages/{stageId}", new SaveStageRequest("Котлован", 101, 10));
        var notImage = await content.PostAsync($"/api/v1/stages/{stageId}/photos", PhotoForm("<script>"u8.ToArray(), "фото.png", null));
        var pdf = await content.PostAsync($"/api/v1/stages/{stageId}/photos", PhotoForm("%PDF-1.4"u8.ToArray(), "фото.pdf", null));

        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notImage.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pdf.StatusCode);
    }

    private Task<Guid> SeededBuildingIdAsync() =>
        factory.WithDbAsync(db => db.Buildings.Where(b => b.NameRu == CatalogSeeder.BuildingName).Select(b => b.Id).SingleAsync());

    private static MultipartFormDataContent PhotoForm(byte[] bytes, string fileName, string? caption)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        if (caption is not null)
            form.Add(new StringContent(caption), "caption");
        return form;
    }
}

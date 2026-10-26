using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

public class ApartmentImportTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Import_CreatesFloorsAndApartments_ThenUpdatesExisting()
    {
        var client = await factory.CreateContentClientAsync();
        var buildingId = await CreateBuildingAsync("Импорт");

        var first = await client.PostAsync($"/api/v1/apartments/import?buildingId={buildingId}", Excel(
            [1, 101, 1, 40, 9000, 0, "SR-101"],
            [1, 102, 2, 60.5, 9000, 1],
            [2, 201, 0, 30, 9500, 0]));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<ImportResult>(TestJson.Options);
        Assert.Equal((3, 0), (firstResult!.Created, firstResult.Updated));

        // Пустой столбец G не стирает уже заданный ID в 3D-модели.
        var second = await client.PostAsync($"/api/v1/apartments/import?buildingId={buildingId}", Excel(
            [1, 101, 1, 40, 9900, 0],
            [1, 102, 2, 60.5, 9000, 1],
            [1, 103, 3, 80, 9000, 2, "SR-103"]));

        var secondResult = await second.Content.ReadFromJsonAsync<ImportResult>(TestJson.Options);
        Assert.Equal((1, 1), (secondResult!.Created, secondResult.Updated));

        var apartments = await factory.WithDbAsync(db => db.Apartments
            .Where(a => a.Floor.BuildingId == buildingId)
            .OrderBy(a => a.Number)
            .Select(a => new { a.Number, a.Price, FloorNumber = a.Floor.Number, a.PlanSlot, a.Model3dId })
            .ToListAsync());
        Assert.Equal([101, 102, 103, 201], apartments.Select(a => a.Number));
        Assert.Equal(396000m, apartments[0].Price);
        Assert.Equal(544500m, apartments[1].Price);
        Assert.Equal(2, apartments[3].FloorNumber);
        Assert.Equal([0, 1, 2, 0], apartments.Select(a => a.PlanSlot));
        Assert.Equal(["SR-101", null, "SR-103", null], apartments.Select(a => a.Model3dId));
    }

    [Fact]
    public async Task Import_RejectsOccupiedOrDuplicatePlanSlots()
    {
        var client = await factory.CreateContentClientAsync();
        var buildingId = await CreateBuildingAsync("Импорт позиций");
        (await client.PostAsync($"/api/v1/apartments/import?buildingId={buildingId}", Excel(
            [1, 101, 1, 40, 9000, 0],
            [1, 102, 1, 40, 9000, 1]))).EnsureSuccessStatusCode();

        var response = await client.PostAsync($"/api/v1/apartments/import?buildingId={buildingId}", Excel(
            [1, 103, 1, 40, 9000, 0],
            [1, 104, 1, 40, 9000, 5],
            [1, 105, 1, 40, 9000, 5],
            [1, 106, 1, 40, 9000, 9],
            [1, 102, 1, 40, 9000, 3]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>(TestJson.Options);
        Assert.Equal([2, 4, 5], result!.Errors.Select(e => e.Row));
        Assert.Contains("уже занята квартирой №101", result.Errors[0].Message);
        Assert.Contains("уже занята выше в файле", result.Errors[1].Message);
        Assert.Contains("от 0 до 7", result.Errors[2].Message);

        // Квартира 102 переезжает на позицию 3, а её прежняя позиция 1 освобождается — это допустимо.
        var move = await client.PostAsync($"/api/v1/apartments/import?buildingId={buildingId}", Excel(
            [1, 102, 1, 40, 9000, 3],
            [1, 103, 1, 40, 9000, 1]));
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
    }

    [Fact]
    public async Task Import_WithErrors_Returns422AndSavesNothing()
    {
        var client = await factory.CreateContentClientAsync();
        var buildingId = await CreateBuildingAsync("Импорт с ошибками");

        var response = await client.PostAsync($"/api/v1/apartments/import?buildingId={buildingId}", Excel(
            [1, 101, 1, 40, 9000, 0],
            [9, 901, 1, 40, 9000, 0],
            [1, 102, 1, "сорок", 9000, 1],
            [1, 101, 2, 50, 9000, 2]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>(TestJson.Options);
        Assert.Equal([3, 4, 5], result!.Errors.Select(e => e.Row));
        Assert.Contains("этаж должен быть от 0 до 3", result.Errors[0].Message);
        Assert.Contains("площадь", result.Errors[1].Message);

        var floors = await factory.WithDbAsync(db => db.Floors.CountAsync(f => f.BuildingId == buildingId));
        Assert.Equal(0, floors);
    }

    [Fact]
    public async Task Import_NotXlsx_Returns422()
    {
        var client = await factory.CreateContentClientAsync();
        var buildingId = await CreateBuildingAsync("Импорт не Excel");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("этаж;номер"), "file", "apartments.csv");

        var response = await client.PostAsync($"/api/v1/apartments/import?buildingId={buildingId}", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Import_ForbiddenForManager()
    {
        var content = await factory.CreateContentClientAsync();
        var buildingId = await CreateBuildingAsync("Импорт менеджером");
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.PostAsync($"/api/v1/apartments/import?buildingId={buildingId}", Excel([1, 101, 1, 40, 9000, 0]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Дома создаёт владелец; импорт квартир — контент-менеджер.
    private async Task<Guid> CreateBuildingAsync(string name)
    {
        var response = await (await factory.CreateAdminClientAsync()).PostAsJsonAsync("/api/v1/buildings", new SaveBuildingRequest(name, name, "г. Душанбе", 3, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BuildingResponse>(TestJson.Options))!.Id;
    }

    private static MultipartFormDataContent Excel(params object[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Квартиры");
        string[] headers = ["Этаж", "Номер", "Комнат", "Площадь", "Цена за м²", "Позиция на плане", "ID в 3D-модели"];
        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(1, col + 1).Value = headers[col];
        for (var row = 0; row < rows.Length; row++)
        for (var col = 0; col < rows[row].Length; col++)
            sheet.Cell(row + 2, col + 1).Value = XLCellValue.FromObject(rows[row][col]);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { file, "file", "apartments.xlsx" } };
    }
}

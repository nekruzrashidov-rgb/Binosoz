using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

public class CatalogTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task SeededBuilding_Has16ResidentialFloorsAnd128ApartmentsWithStatuses()
    {
        var building = await GetSeededBuildingAsync();
        var floors = await GetAsync<PagedResult<FloorResponse>>($"/api/v1/buildings/{building.Id}/floors?pageSize=100");

        Assert.Equal(17, floors.TotalCount);
        var commercial = Assert.Single(floors.Items, f => f.IsCommercial);
        Assert.Equal(0, commercial.Number);
        Assert.Equal(0, commercial.TotalApartments);

        var residential = floors.Items.Where(f => !f.IsCommercial).ToList();
        Assert.Equal(Enumerable.Range(1, 16), residential.Select(f => f.Number));

        var apartments = new List<ApartmentResponse>();
        foreach (var floor in residential)
        {
            var page = await GetAsync<PagedResult<ApartmentResponse>>($"/api/v1/floors/{floor.Id}/apartments?pageSize=100");
            Assert.Equal(8, page.Items.Count);
            Assert.Equal(page.Items.Count(a => a.Status == ApartmentStatus.Free), floor.FreeApartments);
            apartments.AddRange(page.Items);
        }

        Assert.Equal(128, apartments.Count);
        Assert.Equal(128, building.TotalApartments);
        Assert.Equal(apartments.Count(a => a.Status == ApartmentStatus.Free), building.FreeApartments);
        Assert.Contains(apartments, a => a.Status == ApartmentStatus.Free);
        Assert.Contains(apartments, a => a.Status == ApartmentStatus.Sold);

        // Позиции на плане: по порядку номеров на этаже, 0–7, без повторов.
        Assert.All(apartments.GroupBy(a => a.FloorNumber), floor =>
            Assert.Equal(Enumerable.Range(0, 8), floor.OrderBy(a => a.Number).Select(a => a.PlanSlot)));
        Assert.All(apartments, a => Assert.Null(a.Model3dId));
    }

    [Theory]
    [InlineData(5, 1, 46, 9675, 445100)]
    [InlineData(16, 3, 92, 10720, 986200)]
    [InlineData(1, 5, 34, 9295, 316000)]
    public async Task SeededPrices_FollowFloorFormula(int floorNumber, int position, decimal area, decimal pricePerM2, decimal price)
    {
        var apartment = await FindSeededApartmentAsync(floorNumber * 100 + position);

        Assert.Equal(area, apartment.Area);
        Assert.Equal(pricePerM2, apartment.PricePerM2);
        Assert.Equal(price, apartment.Price);
        Assert.Equal(Currency.TJS, apartment.Currency);
    }

    [Fact]
    public async Task SeededStatuses_LowerFloorsHaveMoreSold()
    {
        var sold = await factory.WithDbAsync(db => db.Apartments
            .Where(a => a.Floor.Building.NameRu == CatalogSeeder.BuildingName && a.Status == ApartmentStatus.Sold)
            .Select(a => a.Floor.Number)
            .ToListAsync());

        Assert.True(sold.Count(f => f <= 4) > sold.Count(f => f >= 13));
    }

    [Fact]
    public async Task UnknownBuilding_Returns404WithRussianMessage()
    {
        var response = await _guest.GetAsync($"/api/v1/buildings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Дом не найден", problem?.Detail);
    }

    [Fact]
    public async Task CreateBuilding_RequiresContentOrAdmin()
    {
        var request = NewBuilding("Права");

        var guest = await _guest.PostAsJsonAsync("/api/v1/buildings", request);
        var manager = await (await factory.CreateManagerClientAsync()).PostAsJsonAsync("/api/v1/buildings", request);
        var content = await (await factory.CreateContentClientAsync()).PostAsJsonAsync("/api/v1/buildings", request);
        var admin = await (await factory.CreateClientForAsync(SitoraApiFactory.AdminEmail, factory.AdminPassword))
            .PostAsJsonAsync("/api/v1/buildings", NewBuilding("Права админа"));

        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
        Assert.Equal(HttpStatusCode.Created, content.StatusCode);
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
    }

    [Fact]
    public async Task CreateBuilding_InvalidRequest_Returns400()
    {
        var client = await factory.CreateContentClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/buildings", NewBuilding("") with { FloorsCount = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Укажите название на русском", body);
        Assert.Contains("Этажность должна быть от 1 до 100", body);
    }

    [Fact]
    public async Task BuildingDescription_DependsOnAcceptLanguage_WithRussianFallback()
    {
        var client = await factory.CreateContentClientAsync();
        var both = await ReadAsync<BuildingResponse>(await client.PostAsJsonAsync("/api/v1/buildings",
            NewBuilding("Описание на двух языках") with { DescriptionRu = "Описание по-русски", DescriptionTg = "Тавсиф (тест)" }));
        var ruOnly = await ReadAsync<BuildingResponse>(await client.PostAsJsonAsync("/api/v1/buildings",
            NewBuilding("Описание только по-русски") with { DescriptionRu = "Только русский текст" }));

        Assert.Equal(("Описание по-русски", "Тавсиф (тест)"), (both.DescriptionRu, both.DescriptionTg));
        Assert.Equal("Тавсиф (тест)", (await GetInTajikAsync(both.Id)).Description);
        Assert.Equal("Описание по-русски", (await GetAsync<BuildingResponse>($"/api/v1/buildings/{both.Id}")).Description);

        // Без таджикского перевода показываем русский текст, а не пустоту.
        var fallback = await GetInTajikAsync(ruOnly.Id);
        Assert.Equal("Только русский текст", fallback.Description);
        Assert.Null(fallback.DescriptionTg);

        var updated = await ReadAsync<BuildingResponse>(await client.PutAsJsonAsync($"/api/v1/buildings/{ruOnly.Id}",
            NewBuilding("Описание только по-русски") with { DescriptionRu = "Только русский текст", DescriptionTg = "Перевод добавлен" }));
        Assert.Equal("Перевод добавлен", updated.DescriptionTg);
        Assert.Equal("Перевод добавлен", (await GetInTajikAsync(ruOnly.Id)).Description);
    }

    [Fact]
    public async Task BuildingName_DependsOnAcceptLanguage()
    {
        var client = await factory.CreateContentClientAsync();
        var created = await ReadAsync<BuildingResponse>(await client.PostAsJsonAsync("/api/v1/buildings",
            NewBuilding("Дом у реки") with { NameTg = "Хонаи назди дарё" }));

        using var tgRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/buildings/{created.Id}");
        tgRequest.Headers.AcceptLanguage.ParseAdd("tg");
        var tg = await ReadAsync<BuildingResponse>(await _guest.SendAsync(tgRequest));
        var ru = await GetAsync<BuildingResponse>($"/api/v1/buildings/{created.Id}");

        Assert.Equal("Хонаи назди дарё", tg.Name);
        Assert.Equal("Дом у реки", ru.Name);
    }

    [Fact]
    public async Task ContentCrud_ChecksFloorRangeAndDuplicates_AndComputesPrice()
    {
        var client = await factory.CreateContentClientAsync();
        var building = await ReadAsync<BuildingResponse>(await client.PostAsJsonAsync("/api/v1/buildings", NewBuilding("CRUD")));

        var floorResponse = await client.PostAsJsonAsync("/api/v1/floors", new CreateFloorRequest(building.Id, 2, null));
        Assert.Equal(HttpStatusCode.Created, floorResponse.StatusCode);
        var floor = await ReadAsync<FloorResponse>(floorResponse);

        var tooHigh = await client.PostAsJsonAsync("/api/v1/floors", new CreateFloorRequest(building.Id, 6, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooHigh.StatusCode);

        var duplicateFloor = await client.PostAsJsonAsync("/api/v1/floors", new CreateFloorRequest(building.Id, 2, null));
        Assert.Equal(HttpStatusCode.Conflict, duplicateFloor.StatusCode);

        var apartmentResponse = await client.PostAsJsonAsync("/api/v1/apartments", new CreateApartmentRequest(floor.Id, 201, 2, 50.5m, 10000m, 0));
        Assert.Equal(HttpStatusCode.Created, apartmentResponse.StatusCode);
        var apartment = await ReadAsync<ApartmentResponse>(apartmentResponse);
        Assert.Equal(505000m, apartment.Price);
        Assert.Equal(ApartmentStatus.Free, apartment.Status);

        var duplicateApartment = await client.PostAsJsonAsync("/api/v1/apartments", new CreateApartmentRequest(floor.Id, 201, 1, 40m, 10000m, 1));
        Assert.Equal(HttpStatusCode.Conflict, duplicateApartment.StatusCode);
        Assert.Contains("Квартира №201 уже есть", await duplicateApartment.Content.ReadAsStringAsync());

        var occupiedSlot = await client.PostAsJsonAsync("/api/v1/apartments", new CreateApartmentRequest(floor.Id, 202, 1, 40m, 10000m, 0));
        Assert.Equal(HttpStatusCode.Conflict, occupiedSlot.StatusCode);
        Assert.Contains("Позиция 0 на плане этого этажа уже занята", await occupiedSlot.Content.ReadAsStringAsync());

        var badSlot = await client.PostAsJsonAsync("/api/v1/apartments", new CreateApartmentRequest(floor.Id, 203, 1, 40m, 10000m, 8));
        Assert.Equal(HttpStatusCode.BadRequest, badSlot.StatusCode);
        Assert.Contains("от 0 до 7", await badSlot.Content.ReadAsStringAsync());

        var withModel = await ReadAsync<ApartmentResponse>(await client.PostAsJsonAsync("/api/v1/apartments",
            new CreateApartmentRequest(floor.Id, 204, 1, 40m, 10000m, 3, "SR-F2-A204")));
        Assert.Equal((3, "SR-F2-A204"), (withModel.PlanSlot, withModel.Model3dId));

        var floors = await GetAsync<PagedResult<FloorResponse>>($"/api/v1/buildings/{building.Id}/floors");
        var savedFloor = Assert.Single(floors.Items);
        Assert.Equal(2, savedFloor.TotalApartments);
        Assert.Equal(2, savedFloor.FreeApartments);
    }

    [Fact]
    public async Task UpdateApartmentPrice_IsVisibleImmediately_AndWrittenToAudit()
    {
        var client = await factory.CreateContentClientAsync();
        var apartment = await CreateApartmentAsync(client, "Аудит");

        // Первый запрос кладёт квартиру в кэш.
        await GetAsync<ApartmentResponse>($"/api/v1/apartments/{apartment.Id}");

        var update = await client.PutAsJsonAsync($"/api/v1/apartments/{apartment.Id}", new UpdateApartmentRequest(apartment.Number, 3, 60m, 12000m, apartment.PlanSlot, "SR-model-1"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var reloaded = await GetAsync<ApartmentResponse>($"/api/v1/apartments/{apartment.Id}");
        Assert.Equal(720000m, reloaded.Price);
        Assert.Equal(3, reloaded.Rooms);
        Assert.Equal("SR-model-1", reloaded.Model3dId);

        var entry = await factory.WithDbAsync(db => db.AuditLogs.SingleAsync(a => a.EntityId == apartment.Id && a.Action == "Updated"));
        Assert.NotNull(entry.UserId);
        Assert.Contains("\"Price\": 500000", entry.Before);
        Assert.Contains("\"Price\": 720000", entry.After);
    }

    [Fact]
    public async Task OnRequest_TogglesStatusAndFreeCounter()
    {
        var client = await factory.CreateContentClientAsync();
        var apartment = await CreateApartmentAsync(client, "Под запрос");

        var onRequest = await ReadAsync<ApartmentResponse>(await client.PatchAsJsonAsync(
            $"/api/v1/apartments/{apartment.Id}/on-request", new SetOnRequestRequest(true)));
        Assert.Equal(ApartmentStatus.OnRequest, onRequest.Status);

        var floors = await GetAsync<PagedResult<FloorResponse>>($"/api/v1/buildings/{apartment.BuildingId}/floors");
        Assert.Equal(0, floors.Items.Single().FreeApartments);

        var again = await client.PatchAsJsonAsync($"/api/v1/apartments/{apartment.Id}/on-request", new SetOnRequestRequest(true));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);

        var back = await ReadAsync<ApartmentResponse>(await client.PatchAsJsonAsync(
            $"/api/v1/apartments/{apartment.Id}/on-request", new SetOnRequestRequest(false)));
        Assert.Equal(ApartmentStatus.Free, back.Status);

        var audit = await factory.WithDbAsync(db => db.AuditLogs.CountAsync(a => a.EntityId == apartment.Id && a.Action == "StatusChanged"));
        Assert.Equal(2, audit);
    }

    [Fact]
    public async Task OnRequest_ForbiddenForManager()
    {
        var apartment = await FindSeededApartmentAsync(1601);
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.PatchAsJsonAsync($"/api/v1/apartments/{apartment.Id}/on-request", new SetOnRequestRequest(true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static SaveBuildingRequest NewBuilding(string name) =>
        new(name, name, "г. Душанбе, тест", 5, null, null);

    private static async Task<ApartmentResponse> CreateApartmentAsync(HttpClient client, string buildingName)
    {
        var building = await ReadAsync<BuildingResponse>(await client.PostAsJsonAsync("/api/v1/buildings", NewBuilding(buildingName)));
        var floor = await ReadAsync<FloorResponse>(await client.PostAsJsonAsync("/api/v1/floors", new CreateFloorRequest(building.Id, 1, null)));
        return await ReadAsync<ApartmentResponse>(await client.PostAsJsonAsync("/api/v1/apartments", new CreateApartmentRequest(floor.Id, 101, 2, 50m, 10000m, 0)));
    }

    private async Task<BuildingResponse> GetSeededBuildingAsync()
    {
        var buildings = await GetAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?pageSize=100");
        return buildings.Items.Single(b => b.NameRu == CatalogSeeder.BuildingName);
    }

    private async Task<ApartmentResponse> FindSeededApartmentAsync(int number)
    {
        var id = await factory.WithDbAsync(db => db.Apartments
            .Where(a => a.Floor.Building.NameRu == CatalogSeeder.BuildingName && a.Number == number)
            .Select(a => a.Id)
            .SingleAsync());
        return await GetAsync<ApartmentResponse>($"/api/v1/apartments/{id}");
    }

    private async Task<BuildingResponse> GetInTajikAsync(Guid buildingId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/buildings/{buildingId}");
        request.Headers.AcceptLanguage.ParseAdd("tg");
        return await ReadAsync<BuildingResponse>(await _guest.SendAsync(request));
    }

    private async Task<T> GetAsync<T>(string url) => (await _guest.GetFromJsonAsync<T>(url, TestJson.Options))!;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }
}

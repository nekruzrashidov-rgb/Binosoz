using System.Net.Http.Json;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Кэш HTTP-ответов публичного каталога: повторный GET без входа отдаётся из кэша (заголовок Age), изменение
// квартиры сбрасывает его тегом, ответы со входом не кэшируются и помечены private, no-store.
public class OutputCacheTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task AnonymousCatalogGet_IsCached_AndEvictedOnChange()
    {
        var id = await factory.FindFreeApartmentAsync();
        var guest = factory.CreateClient();

        var first = await guest.GetAsync($"/api/v1/apartments/{id}");
        first.EnsureSuccessStatusCode();
        Assert.Equal("public, max-age=15", first.Headers.CacheControl?.ToString());
        Assert.Null(first.Headers.Age);

        var second = await guest.GetAsync($"/api/v1/apartments/{id}");
        Assert.NotNull(second.Headers.Age);

        // Квартира уходит «под запрос» — кэш каталога сбрасывается, гость сразу видит новый статус.
        var content = await factory.CreateContentClientAsync();
        (await content.PatchAsJsonAsync($"/api/v1/apartments/{id}/on-request", new SetOnRequestRequest(true))).EnsureSuccessStatusCode();

        var third = await guest.GetAsync($"/api/v1/apartments/{id}");
        Assert.Null(third.Headers.Age);
        var apartment = await third.Content.ReadFromJsonAsync<ApartmentResponse>(TestJson.Options);
        Assert.Equal("OnRequest", apartment!.Status.ToString());

        (await content.PatchAsJsonAsync($"/api/v1/apartments/{id}/on-request", new SetOnRequestRequest(false))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AuthorizedGet_IsNotCached_AndPrivate()
    {
        var id = await factory.FindFreeApartmentAsync();
        var manager = await factory.CreateManagerClientAsync();

        await manager.GetAsync($"/api/v1/apartments/{id}");
        var again = await manager.GetAsync($"/api/v1/apartments/{id}");

        Assert.Null(again.Headers.Age);
        Assert.True(again.Headers.CacheControl?.Private);
        Assert.True(again.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task AcceptLanguage_IsPartOfTheKey()
    {
        var id = await factory.FindFreeApartmentAsync();
        var guest = factory.CreateClient();

        await guest.GetAsync($"/api/v1/apartments/{id}");
        using var tg = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/apartments/{id}");
        tg.Headers.AcceptLanguage.ParseAdd("tg");
        var response = await guest.SendAsync(tg);

        Assert.Null(response.Headers.Age);
    }
}

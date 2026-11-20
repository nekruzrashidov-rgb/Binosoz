using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Sales;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// A-15: за доверенным прокси адрес клиента можно брать из его заголовка (CF-Connecting-IP у Cloudflare перед Render) —
// настройка ForwardedHeaders:ClientIpHeader, по умолчанию выключена. Видно по IP, который запоминает бронь с сайта.
public class ClientIpHeaderFactory : SitoraApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ForwardedHeaders:Enabled", "true");
        builder.UseSetting("ForwardedHeaders:ClientIpHeader", "CF-Connecting-IP");
    }
}

public class ClientIpHeaderTests(ClientIpHeaderFactory factory) : IClassFixture<ClientIpHeaderFactory>
{
    [Fact]
    public async Task ClientIp_ComesFromTheConfiguredHeader_InvalidValueIsIgnored()
    {
        Assert.Equal("203.0.113.7", await BookAndReadIpAsync("203.0.113.7"));
        Assert.NotEqual("not-an-ip", await BookAndReadIpAsync("not-an-ip"));
    }

    private async Task<string?> BookAndReadIpAsync(string header)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("CF-Connecting-IP", header);
        var response = await client.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость", SitoraApiFactory.RandomPhone(), null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = (await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        return await factory.WithDbAsync(db => db.Bookings.IgnoreQueryFilters()
            .Where(b => b.Id == booking.Id).Select(b => b.ClientIp).SingleAsync());
    }
}

// Без настройки заголовок игнорируется: подставить себе адрес и обойти лимиты нельзя.
public class ClientIpHeaderOffTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task WithoutSetting_HeaderIsIgnored()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.8");
        var response = await client.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость", SitoraApiFactory.RandomPhone(), null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = (await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        var ip = await factory.WithDbAsync(db => db.Bookings.IgnoreQueryFilters()
            .Where(b => b.Id == booking.Id).Select(b => b.ClientIp).SingleAsync());
        Assert.NotEqual("203.0.113.8", ip);
    }
}

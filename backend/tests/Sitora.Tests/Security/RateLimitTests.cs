using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Sales;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Фабрика с маленькими лимитами. IP клиента задаётся заголовком X-Test-Client-IP,
// чтобы тесты не делили между собой один счётчик.
public class LowRateLimitFactory : SitoraApiFactory
{
    public const string ClientIpHeader = "X-Test-Client-IP";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:LoginPerMinute", "3");
        builder.UseSetting("RateLimiting:SmsPerHour", "2");
        builder.UseSetting("RateLimiting:PublicFormsPerHour", "2");
        builder.UseSetting("RateLimiting:ApplicationsPerHour", "3");
        builder.UseSetting("RateLimiting:AnonymousPerMinute", "20");
        builder.UseSetting("RateLimiting:UserPerMinute", "1000");
        builder.UseSetting("Booking:GuestActivePerIp", "1");
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestClientIpFilter>());
    }

    public HttpClient CreateClientWithIp(string ip)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ClientIpHeader, ip);
        return client;
    }

    private class TestClientIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[ClientIpHeader], out var ip))
                    context.Connection.RemoteIpAddress = ip;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

public class RateLimitTests(LowRateLimitFactory factory) : IClassFixture<LowRateLimitFactory>
{
    [Fact]
    public async Task Login_IsLimitedPerIp_With429InRussian()
    {
        var client = factory.CreateClientWithIp("10.0.0.1");
        var request = new LoginRequest("nobody@sitora.local", "Wrong-password-1");

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", request)).StatusCode);

        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("Слишком много запросов", await limited.Content.ReadAsStringAsync());
        Assert.True(limited.Headers.RetryAfter is not null);

        // Другой IP считается отдельно.
        var other = await factory.CreateClientWithIp("10.0.0.2").PostAsJsonAsync("/api/v1/auth/login", request);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    [Fact]
    public async Task GeneralLimit_AppliesToGuestsPerIp_AndToUsersSeparately_ButNotToHealth()
    {
        var guest = factory.CreateClientWithIp("10.0.3.1");
        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/v1/buildings?organization=nekruz")).StatusCode);

        // Ответы каталога кэшируются, но лимит по IP стоит раньше кэша: 21-й запрос гостя — 429 с Retry-After.
        var limited = await guest.GetAsync("/api/v1/buildings?organization=nekruz");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Contains("Слишком много запросов", await limited.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/health/ready")).StatusCode);

        // Сотрудник с того же IP считается по своей учётной записи, а не по IP.
        var staff = factory.CreateClientWithIp("10.0.3.2");
        var login = await staff.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.ManagerEmail, factory.ManagerPassword));
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var manager = factory.CreateClientWithIp("10.0.3.1");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/v1/buildings?organization=nekruz")).StatusCode);
    }

    [Fact]
    public async Task SmsSend_IsLimitedPerIp()
    {
        var client = factory.CreateClientWithIp("10.0.1.1");

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(SitoraApiFactory.RandomPhone()));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(SitoraApiFactory.RandomPhone()));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task DeveloperApplications_AreLimitedToThreePerIpPerHour()
    {
        var guest = factory.CreateClientWithIp("10.0.4.1");
        SubmitApplicationRequest Application() =>
            new("ООО Лимит", $"limit-{Guid.NewGuid():N}@company.test", "+992901112233", "Тест Лимитов", null);

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/api/v1/public/applications", Application())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await guest.PostAsJsonAsync("/api/v1/public/applications", Application())).StatusCode);

        // Счётчик свой: заявки на квартиры с того же IP не задеты.
        Assert.Equal(HttpStatusCode.Created, (await guest.PostAsJsonAsync("/api/v1/leads",
            new CreateLeadRequest("Гость", "+992901112233", null, null, null))).StatusCode);
    }

    [Fact]
    public async Task PublicForms_LimitGuests_ButNotStaff()
    {
        var guest = factory.CreateClientWithIp("10.0.2.1");
        var lead = new CreateLeadRequest("Гость", "+992901112233", null, null, null);

        Assert.Equal(HttpStatusCode.Created, (await guest.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await guest.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await guest.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);

        var manager = factory.CreateClientWithIp("10.0.2.1");
        var login = await manager.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.ManagerEmail, factory.ManagerPassword));
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        manager.DefaultRequestHeaders.Authorization = new("Bearer", token);
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
    }

    // Вход покупателя не снимает лимит публичных форм: иначе любой зарегистрировавшийся бронировал бы без ограничений.
    [Fact]
    public async Task PublicForms_LimitSignedInCustomers_LikeGuests()
    {
        var manager = await factory.CreateManagerClientAsync();
        var customer = await Sales.BookingTests.CreateCustomerAsync(manager);
        var client = await factory.CreateCustomerClientAsync(customer.Phone);
        client.DefaultRequestHeaders.Add(ClientIpHeaderName, "10.0.5.1");
        var lead = new CreateLeadRequest("Покупатель", "+992901112244", null, null, null);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
    }

    // Booking:GuestActivePerIp = 1 в этой фабрике: вторая бронь с сайта с того же IP (другой телефон) — 429,
    // с другого IP — можно. Бронь запоминает IP.
    [Fact]
    public async Task GuestBookings_AreLimitedByActiveBookingsPerIp()
    {
        var guest = factory.CreateClientWithIp("10.0.6.1");
        var first = await guest.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость", SitoraApiFactory.RandomPhone(), null, null));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await guest.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость", SitoraApiFactory.RandomPhone(), null, null));
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Contains("С этого адреса уже есть несколько активных броней", await second.Content.ReadAsStringAsync());

        var other = await factory.CreateClientWithIp("10.0.6.2").PostAsJsonAsync(
            $"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость", SitoraApiFactory.RandomPhone(), null, null));
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);

        var booking = (await first.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        var ip = await factory.WithDbAsync(db => db.Bookings.IgnoreQueryFilters()
            .Where(b => b.Id == booking.Id).Select(b => b.ClientIp).SingleAsync());
        Assert.Equal("10.0.6.1", ip);
    }

    // A-19: просроченная бронь, которую фоновая задача ещё не сняла (статус Active, срок прошёл), не занимает ни лимит
    // по IP, ни лимит «одна бронь на телефон».
    [Fact]
    public async Task ExpiredButNotYetReleasedBooking_DoesNotCountTowardsGuestLimits()
    {
        var guest = factory.CreateClientWithIp("10.0.6.9");
        var phone = SitoraApiFactory.RandomPhone();
        var first = await guest.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость", phone, null, null));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var booking = (await first.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;

        await factory.WithDbAsync(db => db.Bookings.IgnoreQueryFilters().Where(b => b.Id == booking.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        // Тот же IP и тот же телефон, другая квартира: лимиты считают только действующие брони.
        var again = await guest.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость", phone, null, null));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    private const string ClientIpHeaderName = LowRateLimitFactory.ClientIpHeader;
}

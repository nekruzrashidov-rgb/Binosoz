using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

// Бизнес-правила, которые не должны зависеть от фоновых задач и от удачи: просроченная бронь, платежи без удаления,
// аудит в той же транзакции, что и изменение.
public class BusinessRuleTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task ExpiredBooking_WithoutTheJob_DoesNotBlockTheApartment()
    {
        var apartmentId = await factory.FindFreeApartmentAsync();
        var guest = factory.CreateClient();
        var first = await guest.PostAsJsonAsync($"/api/v1/apartments/{apartmentId}/bookings",
            new CreateBookingRequest("Первый", SitoraApiFactory.RandomPhone(), null, null));
        first.EnsureSuccessStatusCode();
        var expired = (await first.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;

        // Срок брони прошёл, а задача ExpireBookingsJob не запускалась (сервис спал).
        await factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == expired.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        var apartment = await guest.GetFromJsonAsync<ApartmentResponse>($"/api/v1/apartments/{apartmentId}", TestJson.Options);
        Assert.Equal(ApartmentStatus.Free, apartment!.Status);

        var second = await guest.PostAsJsonAsync($"/api/v1/apartments/{apartmentId}/bookings",
            new CreateBookingRequest("Второй", SitoraApiFactory.RandomPhone(), null, null));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(BookingStatus.Expired, (await db.Bookings.SingleAsync(b => b.Id == expired.Id)).Status);
            Assert.Equal(1, await db.Bookings.CountAsync(b => b.ApartmentId == apartmentId && b.Status == BookingStatus.Active));
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == expired.Id && a.Action == "Expired"));
        });
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task Payments_CannotBeDeletedOrEdited_ByAnyMethod(string method)
    {
        var accountant = await factory.CreateAccountantClientAsync();
        var paymentId = await factory.WithDbAsync(db => db.Payments.Select(p => p.Id).FirstOrDefaultAsync());

        var response = await accountant.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/api/v1/payments/{paymentId}"));

        Assert.True(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"{method}: {(int)response.StatusCode}");
    }

    // Изменение и его запись в AuditLog сохраняются одним SaveChanges: откат транзакции убирает оба, успех оставляет оба.
    [Fact]
    public async Task Audit_IsWrittenInTheSameTransaction_AsTheChange()
    {
        var apartmentId = await factory.FindFreeApartmentAsync();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await SignInAsContentAsync(services);
        var db = services.GetRequiredService<AppDbContext>();
        var catalog = services.GetRequiredService<ICatalogService>();
        var auditBefore = await db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.EntityId == apartmentId);
        var before = await db.Apartments.AsNoTracking().SingleAsync(a => a.Id == apartmentId);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await catalog.SetOnRequestAsync(apartmentId, true);
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Equal(before.Status, (await db.Apartments.AsNoTracking().SingleAsync(a => a.Id == apartmentId)).Status);
        Assert.Equal(auditBefore, await db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.EntityId == apartmentId));

        await catalog.SetOnRequestAsync(apartmentId, true);
        db.ChangeTracker.Clear();
        Assert.Equal(ApartmentStatus.OnRequest, (await db.Apartments.AsNoTracking().SingleAsync(a => a.Id == apartmentId)).Status);
        Assert.Equal(auditBefore + 1, await db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.EntityId == apartmentId));
        await catalog.SetOnRequestAsync(apartmentId, false);
    }

    // Сервисы берут текущего пользователя из HTTP-запроса: в тесте сервиса подставляем контент-менеджера.
    private async Task SignInAsContentAsync(IServiceProvider services)
    {
        var http = services.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var client = await factory.CreateContentClientAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);
        http.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(jwt.Claims, "Bearer", "name", Sitora.Infrastructure.Security.JwtTokenService.RoleClaimType)),
            RequestServices = services
        };
    }
}

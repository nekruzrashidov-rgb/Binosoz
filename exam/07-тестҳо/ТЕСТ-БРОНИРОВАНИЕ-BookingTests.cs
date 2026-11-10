using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Jobs;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

public class BookingTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task GuestBooking_BooksApartmentForTwoDays_AndAppearsInPipeline()
    {
        var apartmentId = await factory.FindFreeApartmentAsync();
        var phone = SitoraApiFactory.RandomPhone();

        var response = await BookAsGuestAsync(apartmentId, phone);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await ReadAsync<BookingResponse>(response);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.NotNull(booking.LeadId);
        Assert.InRange(booking.ExpiresAt - booking.CreatedAt, TimeSpan.FromHours(47.9), TimeSpan.FromHours(48.1));

        var apartment = await _guest.GetFromJsonAsync<ApartmentResponse>($"/api/v1/apartments/{apartmentId}", TestJson.Options);
        Assert.Equal(ApartmentStatus.Booked, apartment!.Status);

        var manager = await factory.CreateManagerClientAsync();
        var pipeline = await manager.GetFromJsonAsync<PipelineResponse>("/api/v1/crm/pipeline", TestJson.Options);
        var bookingColumn = pipeline!.Columns.Single(c => c.Stage == DealStage.Booking);
        var card = Assert.Single(bookingColumn.Cards, c => c.Id == booking.Id);
        Assert.Equal(PipelineCardKind.Booking, card.Kind);
        Assert.Equal(phone, card.Phone);

        // Заявка с активной бронью не дублируется в колонке «Заявка».
        var leadColumn = pipeline.Columns.Single(c => c.Stage == DealStage.Lead);
        Assert.DoesNotContain(leadColumn.Cards, c => c.Id == booking.LeadId);

        var notification = await factory.WithDbAsync(db => db.Notifications.AnyAsync(n => n.Text.Contains(phone)));
        Assert.True(notification);
    }

    [Fact]
    public async Task ConcurrentBookings_OnlyOneSucceeds()
    {
        var apartmentId = await factory.FindFreeApartmentAsync();
        const int attempts = 10;

        var responses = await Task.WhenAll(Enumerable.Range(0, attempts)
            .Select(_ => factory.CreateClient().PostAsJsonAsync($"/api/v1/apartments/{apartmentId}/bookings",
                new CreateBookingRequest("Покупатель", SitoraApiFactory.RandomPhone(), null, null))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(attempts - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var active = await factory.WithDbAsync(db =>
            db.Bookings.CountAsync(b => b.ApartmentId == apartmentId && b.Status == BookingStatus.Active));
        Assert.Equal(1, active);
    }

    [Fact]
    public async Task PartialUniqueIndex_RejectsSecondActiveBookingInDatabase()
    {
        var apartmentId = await factory.FindFreeApartmentAsync();
        var leadId = await factory.WithDbAsync(async db =>
        {
            var lead = new Lead { OrganizationId = Organization.DefaultId, Name = "Индекс", Phone = SitoraApiFactory.RandomPhone() };
            db.Leads.Add(lead);
            await db.SaveChangesAsync();
            return lead.Id;
        });

        // В обход сервиса: даже без транзакции база не даст создать две активные брони.
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Bookings.Add(new Booking { ApartmentId = apartmentId, LeadId = leadId, ExpiresAt = DateTime.UtcNow.AddDays(1) });
            db.Bookings.Add(new Booking { ApartmentId = apartmentId, LeadId = leadId, ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
            return 0;
        }));
        Assert.Contains("IX_Bookings_ApartmentId_Active", error.InnerException?.Message);

        // Неактивных броней на квартиру может быть сколько угодно.
        await factory.WithDbAsync(async db =>
        {
            for (var i = 0; i < 2; i++)
            {
                var booking = new Booking { ApartmentId = apartmentId, LeadId = leadId, ExpiresAt = DateTime.UtcNow };
                booking.Cancel("тест");
                db.Bookings.Add(booking);
            }
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task BookingSoldApartment_Returns409()
    {
        var soldId = await factory.WithDbAsync(db =>
            db.Apartments.Where(a => a.Status == ApartmentStatus.Sold).Select(a => a.Id).FirstAsync());

        var response = await BookAsGuestAsync(soldId, SitoraApiFactory.RandomPhone());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Guest_CannotHoldTwoBookingsWithOnePhone()
    {
        var phone = SitoraApiFactory.RandomPhone();
        var first = await BookAsGuestAsync(await factory.FindFreeApartmentAsync(), phone);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await BookAsGuestAsync(await factory.FindFreeApartmentAsync(), phone);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("уже есть активная бронь", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Guest_CannotBookForCustomer()
    {
        var response = await _guest.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest(null, null, Guid.NewGuid(), null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_BooksForCustomer()
    {
        var manager = await factory.CreateManagerClientAsync();
        var customer = await CreateCustomerAsync(manager);

        var response = await manager.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest(null, null, customer.Id, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await ReadAsync<BookingResponse>(response);
        Assert.Equal(customer.Id, booking.CustomerId);
        Assert.Null(booking.LeadId);
    }

    [Fact]
    public async Task Extend_OnlyManager_AddsTwoDays()
    {
        var booking = await ReadAsync<BookingResponse>(await BookAsGuestAsync(await factory.FindFreeApartmentAsync(), SitoraApiFactory.RandomPhone()));

        var guest = await _guest.PostAsync($"/api/v1/bookings/{booking.Id}/extend", null);
        var content = await (await factory.CreateContentClientAsync()).PostAsync($"/api/v1/bookings/{booking.Id}/extend", null);
        var manager = await (await factory.CreateManagerClientAsync()).PostAsync($"/api/v1/bookings/{booking.Id}/extend", null);

        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, content.StatusCode);
        Assert.Equal(HttpStatusCode.OK, manager.StatusCode);
        var extended = await ReadAsync<BookingResponse>(manager);
        Assert.Equal(booking.ExpiresAt.AddHours(48), extended.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Cancel_RequiresReason_FreesApartment_AndIsAudited()
    {
        var apartmentId = await factory.FindFreeApartmentAsync();
        var booking = await ReadAsync<BookingResponse>(await BookAsGuestAsync(apartmentId, SitoraApiFactory.RandomPhone()));
        var manager = await factory.CreateManagerClientAsync();

        var withoutReason = await manager.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);

        var cancelled = await manager.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest("Клиент передумал"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(BookingStatus.Cancelled, (await ReadAsync<BookingResponse>(cancelled)).Status);

        var apartment = await _guest.GetFromJsonAsync<ApartmentResponse>($"/api/v1/apartments/{apartmentId}", TestJson.Options);
        Assert.Equal(ApartmentStatus.Free, apartment!.Status);

        var again = await manager.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest("ещё раз"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);

        var actions = await factory.WithDbAsync(db => db.AuditLogs
            .Where(a => a.EntityId == booking.Id || a.EntityId == apartmentId)
            .Select(a => a.Entity + ":" + a.Action)
            .ToListAsync());
        Assert.Contains("Booking:Cancelled", actions);
        Assert.Contains("Apartment:StatusChanged", actions);
    }

    [Fact]
    public async Task ExpireJob_ReleasesOverdueBookings_AndKeepsFreshOnes()
    {
        var overdueApartment = await factory.FindFreeApartmentAsync();
        var overdue = await ReadAsync<BookingResponse>(await BookAsGuestAsync(overdueApartment, SitoraApiFactory.RandomPhone()));
        var freshApartment = await factory.FindFreeApartmentAsync();
        var fresh = await ReadAsync<BookingResponse>(await BookAsGuestAsync(freshApartment, SitoraApiFactory.RandomPhone()));

        await factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == overdue.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        await factory.WithServiceAsync<ExpireBookingsJob>(job => job.RunAsync(CancellationToken.None));

        var statuses = await factory.WithDbAsync(db => db.Bookings
            .Where(b => b.Id == overdue.Id || b.Id == fresh.Id)
            .ToDictionaryAsync(b => b.Id, b => b.Status));
        Assert.Equal(BookingStatus.Expired, statuses[overdue.Id]);
        Assert.Equal(BookingStatus.Active, statuses[fresh.Id]);

        var apartments = await factory.WithDbAsync(db => db.Apartments
            .Where(a => a.Id == overdueApartment || a.Id == freshApartment)
            .ToDictionaryAsync(a => a.Id, a => a.Status));
        Assert.Equal(ApartmentStatus.Free, apartments[overdueApartment]);
        Assert.Equal(ApartmentStatus.Booked, apartments[freshApartment]);

        // Снятие брони системой записано в аудит без пользователя.
        var audit = await factory.WithDbAsync(db => db.AuditLogs.SingleAsync(a => a.EntityId == overdue.Id && a.Action == "Expired"));
        Assert.Null(audit.UserId);

        // Каталог сразу показывает квартиру свободной, без ожидания кэша.
        var apartment = await _guest.GetFromJsonAsync<ApartmentResponse>($"/api/v1/apartments/{overdueApartment}", TestJson.Options);
        Assert.Equal(ApartmentStatus.Free, apartment!.Status);
    }

    private Task<HttpResponseMessage> BookAsGuestAsync(Guid apartmentId, string phone) =>
        _guest.PostAsJsonAsync($"/api/v1/apartments/{apartmentId}/bookings", new CreateBookingRequest("Гость", phone, null, null));

    public static async Task<CustomerResponse> CreateCustomerAsync(HttpClient manager, string? phone = null) =>
        await ReadAsync<CustomerResponse>(await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Тестовый Клиент", phone ?? SitoraApiFactory.RandomPhone(), null, "A7654321", null)));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }
}

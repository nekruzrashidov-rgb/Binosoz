using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

public class DealTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task CreateDeal_CopiesPrice_AndIgnoresLaterPriceChanges()
    {
        var manager = await factory.CreateManagerClientAsync();
        var customer = await BookingTests.CreateCustomerAsync(manager);
        var apartmentId = await factory.FindFreeApartmentAsync();
        var apartment = await GetApartmentAsync(apartmentId);

        var deal = await ReadAsync<DealResponse>(await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, customer.Id, null, null, 10000m, "Акция месяца"), TestJson.Options));

        Assert.Equal(DealStage.Lead, deal.Stage);
        Assert.Equal(apartment.Price, deal.Price);
        Assert.Equal(apartment.Price - 10000m, deal.FinalPrice);
        Assert.Equal(Currency.TJS, deal.Currency);

        var content = await factory.CreateContentClientAsync();
        var update = await content.PutAsJsonAsync($"/api/v1/apartments/{apartmentId}",
            new UpdateApartmentRequest(apartment.Number, apartment.Rooms, apartment.Area, apartment.PricePerM2 + 1000m, apartment.PlanSlot));
        update.EnsureSuccessStatusCode();

        var reloaded = await manager.GetFromJsonAsync<DealResponse>($"/api/v1/deals/{deal.Id}", TestJson.Options);
        Assert.Equal(apartment.Price, reloaded!.Price);
    }

    [Fact]
    public async Task StageForwardIsFree_BackwardNeedsReason_AndApartmentFollowsStage()
    {
        var manager = await factory.CreateManagerClientAsync();
        var customer = await BookingTests.CreateCustomerAsync(manager);
        var apartmentId = await factory.FindFreeApartmentAsync();
        var deal = await CreateDealAsync(manager, apartmentId, customer.Id);

        await ChangeStageAsync(manager, deal.Id, DealStage.Showing, null, HttpStatusCode.OK);
        Assert.Equal(ApartmentStatus.Free, (await GetApartmentAsync(apartmentId)).Status);

        await ChangeStageAsync(manager, deal.Id, DealStage.Contract, null, HttpStatusCode.OK);
        Assert.Equal(ApartmentStatus.Sold, (await GetApartmentAsync(apartmentId)).Status);

        await ChangeStageAsync(manager, deal.Id, DealStage.Booking, null, HttpStatusCode.UnprocessableEntity);

        var back = await ChangeStageAsync(manager, deal.Id, DealStage.Booking, "Банк не одобрил ипотеку", HttpStatusCode.OK);
        Assert.Equal("Банк не одобрил ипотеку", back.StageReason);
        Assert.Equal(ApartmentStatus.Booked, (await GetApartmentAsync(apartmentId)).Status);

        await ChangeStageAsync(manager, deal.Id, DealStage.Showing, "Клиент выбирает другую", HttpStatusCode.OK);
        Assert.Equal(ApartmentStatus.Free, (await GetApartmentAsync(apartmentId)).Status);

        var audit = await factory.WithDbAsync(db => db.AuditLogs
            .Where(a => a.EntityId == deal.Id && a.Action == "StageChanged")
            .OrderBy(a => a.At)
            .Select(a => a.After)
            .ToListAsync());
        Assert.Equal(4, audit.Count);
        Assert.Contains("Банк не одобрил ипотеку", audit[2]);
    }

    [Fact]
    public async Task DealFromOwnBooking_ConvertsBooking_AndJobDoesNotFreeApartment()
    {
        var manager = await factory.CreateManagerClientAsync();
        var phone = SitoraApiFactory.RandomPhone();
        var apartmentId = await factory.FindFreeApartmentAsync();
        var guest = factory.CreateClient();
        var booking = await ReadAsync<BookingResponse>(await guest.PostAsJsonAsync($"/api/v1/apartments/{apartmentId}/bookings",
            new CreateBookingRequest("Покупатель", phone, null, null)));

        // Клиента заводят из заявки, по которой была бронь.
        var customer = await ReadAsync<CustomerResponse>(await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Покупатель Сайта", phone, null, null, booking.LeadId)));
        var deal = await CreateDealAsync(manager, apartmentId, customer.Id);

        Assert.Equal(DealStage.Booking, deal.Stage);
        Assert.Equal(booking.Id, deal.BookingId);

        await factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == booking.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExpiresAt, DateTime.UtcNow.AddHours(-1))));
        await factory.WithServiceAsync<Sitora.Infrastructure.Jobs.ExpireBookingsJob>(job => job.RunAsync(CancellationToken.None));

        var state = await factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == booking.Id)
            .Select(b => new { b.Status, ApartmentStatus = b.Apartment.Status, LeadStatus = b.Lead!.Status })
            .SingleAsync());
        Assert.Equal(BookingStatus.Converted, state.Status);
        Assert.Equal(ApartmentStatus.Booked, state.ApartmentStatus);
        Assert.Equal(LeadStatus.Converted, state.LeadStatus);
    }

    [Fact]
    public async Task DealOnApartmentBookedBySomeoneElse_Returns409()
    {
        var manager = await factory.CreateManagerClientAsync();
        var apartmentId = await factory.FindFreeApartmentAsync();
        var bookingResponse = await factory.CreateClient().PostAsJsonAsync($"/api/v1/apartments/{apartmentId}/bookings",
            new CreateBookingRequest("Другой", SitoraApiFactory.RandomPhone(), null, null));
        bookingResponse.EnsureSuccessStatusCode();
        var customer = await BookingTests.CreateCustomerAsync(manager);

        var response = await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, customer.Id, null, null, null, null), TestJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("забронирована другим покупателем", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SecondOpenDealOnApartment_Returns409_ButAllowedAfterCancel()
    {
        var manager = await factory.CreateManagerClientAsync();
        var apartmentId = await factory.FindFreeApartmentAsync();
        var first = await CreateDealAsync(manager, apartmentId, (await BookingTests.CreateCustomerAsync(manager)).Id, DealStage.Booking);
        var otherCustomer = await BookingTests.CreateCustomerAsync(manager);

        var second = await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, otherCustomer.Id, null, null, null, null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await ChangeStageAsync(manager, first.Id, DealStage.Cancelled, null, HttpStatusCode.BadRequest);
        await ChangeStageAsync(manager, first.Id, DealStage.Cancelled, "Отказ клиента", HttpStatusCode.OK);
        Assert.Equal(ApartmentStatus.Free, (await GetApartmentAsync(apartmentId)).Status);
        await ChangeStageAsync(manager, first.Id, DealStage.Lead, "Вернулся", HttpStatusCode.UnprocessableEntity);

        var third = await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, otherCustomer.Id, null, null, null, null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Created, third.StatusCode);
    }

    [Fact]
    public async Task CreateDeal_ValidatesDiscountAndCurrency()
    {
        var manager = await factory.CreateManagerClientAsync();
        var customer = await BookingTests.CreateCustomerAsync(manager);
        var apartmentId = await factory.FindFreeApartmentAsync();

        var noReason = await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, customer.Id, null, null, 5000m, null), TestJson.Options);
        var tooBig = await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, customer.Id, null, null, 100_000_000m, "Слишком много"), TestJson.Options);
        var usd = await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, customer.Id, null, Currency.USD, null, null), TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooBig.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, usd.StatusCode);
    }

    [Fact]
    public async Task Deals_ForbiddenForContent()
    {
        var content = await (await factory.CreateContentClientAsync()).GetAsync("/api/v1/deals");
        var admin = await (await factory.CreateAdminClientAsync()).GetAsync("/api/v1/deals");

        Assert.Equal(HttpStatusCode.Forbidden, content.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    // Бухгалтер находит сделку в списке, чтобы внести первый платёж, но продажи не ведёт.
    [Fact]
    public async Task Accountant_ReadsDeals_ButCannotCreateOrMoveThem()
    {
        var manager = await factory.CreateManagerClientAsync();
        var accountant = await factory.CreateAccountantClientAsync();
        var apartmentId = await factory.FindFreeApartmentAsync();
        var customer = await BookingTests.CreateCustomerAsync(manager);
        var deal = await CreateDealAsync(manager, apartmentId, customer.Id);

        var list = await accountant.GetAsync("/api/v1/deals");
        var card = await accountant.GetAsync($"/api/v1/deals/{deal.Id}");
        var create = await accountant.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, customer.Id, null, null, null, null), TestJson.Options);
        var move = await accountant.PatchAsJsonAsync($"/api/v1/deals/{deal.Id}/stage",
            new ChangeDealStageRequest(DealStage.Contract, null), TestJson.Options);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, move.StatusCode);
    }

    private async Task<ApartmentResponse> GetApartmentAsync(Guid id) =>
        (await factory.CreateClient().GetFromJsonAsync<ApartmentResponse>($"/api/v1/apartments/{id}", TestJson.Options))!;

    private static async Task<DealResponse> CreateDealAsync(HttpClient manager, Guid apartmentId, Guid customerId, DealStage? stage = null) =>
        await ReadAsync<DealResponse>(await manager.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartmentId, customerId, stage, null, null, null), TestJson.Options));

    private static async Task<DealResponse> ChangeStageAsync(HttpClient manager, Guid dealId, DealStage stage, string? reason, HttpStatusCode expected)
    {
        var response = await manager.PatchAsJsonAsync($"/api/v1/deals/{dealId}/stage",
            new ChangeDealStageRequest(stage, reason), TestJson.Options);
        Assert.True(expected == response.StatusCode, $"{stage}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return response.IsSuccessStatusCode ? (await response.Content.ReadFromJsonAsync<DealResponse>(TestJson.Options))! : null!;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }
}

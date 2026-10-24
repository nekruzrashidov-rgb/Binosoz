using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Infrastructure.Integrations;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

public class LeadTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task GuestLead_IsSavedAndSentToNotificationLog()
    {
        var apartmentId = await factory.WithDbAsync(db => db.Apartments
            .Where(a => a.Floor.Building.NameRu == CatalogSeeder.BuildingName && a.Number == 501)
            .Select(a => a.Id)
            .SingleAsync());

        var response = await _guest.PostAsJsonAsync("/api/v1/leads",
            new CreateLeadRequest("Фарход", "90 123 45 67", apartmentId, null, "Позвоните вечером"), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lead = await response.Content.ReadFromJsonAsync<LeadResponse>(TestJson.Options);
        Assert.Equal("+992901234567", lead!.Phone);
        Assert.Equal(LeadStatus.New, lead.Status);
        Assert.Equal(LeadSource.Website, lead.Source);

        var saved = await factory.WithDbAsync(db => db.Leads.SingleAsync(l => l.Id == lead.Id));
        Assert.Equal(apartmentId, saved.ApartmentId);

        var notification = await factory.WithDbAsync(db => db.Notifications
            .SingleAsync(n => n.Text.Contains("+992901234567")));
        Assert.Equal(TelegramNotificationSender.Channel, notification.Channel);
        Assert.Contains("кв. №501", notification.Text);
        Assert.NotNull(notification.SentAt);
    }

    [Fact]
    public async Task GuestLead_WithoutApartment_IsCreated()
    {
        var response = await _guest.PostAsJsonAsync("/api/v1/leads",
            new CreateLeadRequest("Мадина", "+992 93 555 66 77", null, LeadSource.Phone, null), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lead = await response.Content.ReadFromJsonAsync<LeadResponse>(TestJson.Options);
        Assert.Equal(LeadSource.Phone, lead!.Source);
    }

    [Fact]
    public async Task Lead_InvalidPhone_Returns400WithRussianMessage()
    {
        var response = await _guest.PostAsJsonAsync("/api/v1/leads",
            new CreateLeadRequest("Тест", "12345", null, null, null), TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Неверный номер телефона", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lead_UnknownApartment_Returns404()
    {
        var response = await _guest.PostAsJsonAsync("/api/v1/leads",
            new CreateLeadRequest("Тест", "901234567", Guid.NewGuid(), null, null), TestJson.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

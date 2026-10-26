using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

public class CrmTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Summary_MatchesDatabase()
    {
        var manager = await factory.CreateManagerClientAsync();

        var summary = await manager.GetFromJsonAsync<CrmSummaryResponse>("/api/v1/crm/summary", TestJson.Options);

        var apartments = await factory.WithDbAsync(db => db.Apartments.CountAsync());
        var activeBookings = await factory.WithDbAsync(db => db.Bookings.CountAsync(b => b.Status == BookingStatus.Active));
        Assert.Equal(apartments, summary!.ApartmentsByStatus.Values.Sum());
        Assert.Equal(activeBookings, summary.ActiveBookings);
        Assert.True(summary.ActiveBookings >= 2);
        Assert.Equal(1, summary.DealsByStage[DealStage.Contract]);
        Assert.Equal(1, summary.DealsByStage[DealStage.Payment]);
        Assert.True(Assert.Single(summary.SoldAmount).Amount > 0);
    }

    [Fact]
    public async Task Pipeline_HasColumnPerStage_WithSeededDealsAndBookings()
    {
        var admin = await factory.CreateAdminClientAsync();

        var pipeline = await admin.GetFromJsonAsync<PipelineResponse>("/api/v1/crm/pipeline", TestJson.Options);

        Assert.Equal(
            [DealStage.Lead, DealStage.Showing, DealStage.Booking, DealStage.Contract, DealStage.Payment, DealStage.Completed],
            pipeline!.Columns.Select(c => c.Stage));
        var booking = pipeline.Columns.Single(c => c.Stage == DealStage.Booking);
        Assert.Contains(booking.Cards, c => c.Kind == PipelineCardKind.Deal);
        Assert.Contains(booking.Cards, c => c.Kind == PipelineCardKind.Booking && c.ExpiresAt is not null);
        Assert.All(pipeline.Columns, c => Assert.Equal(c.Count, c.Cards.Count));
    }

    [Fact]
    public async Task Crm_ForbiddenForContent()
    {
        var content = await factory.CreateContentClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await content.GetAsync("/api/v1/crm/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await content.GetAsync("/api/v1/crm/pipeline")).StatusCode);
    }

    [Fact]
    public async Task Leads_ManagerListsAndUpdates_WithAudit()
    {
        var lead = await (await factory.CreateClient().PostAsJsonAsync("/api/v1/leads",
                new CreateLeadRequest("Заявка CRM", SitoraApiFactory.RandomPhone(), null, null, null), TestJson.Options))
            .Content.ReadFromJsonAsync<LeadResponse>(TestJson.Options);
        var manager = await factory.CreateManagerClientAsync();
        var managerId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == SitoraApiFactory.ManagerEmail).Select(u => u.Id).SingleAsync());

        var list = await manager.GetFromJsonAsync<PagedResult<LeadResponse>>("/api/v1/leads?status=New&pageSize=100", TestJson.Options);
        Assert.Contains(list!.Items, l => l.Id == lead!.Id);

        var updated = await manager.PatchAsJsonAsync($"/api/v1/leads/{lead!.Id}",
            new UpdateLeadRequest(LeadStatus.InProgress, managerId, "Перезвонить"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<LeadResponse>(TestJson.Options);
        Assert.Equal((LeadStatus.InProgress, managerId), (body!.Status, body.ManagerId));

        var contentId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == SitoraApiFactory.ContentEmail).Select(u => u.Id).SingleAsync());
        var wrongManager = await manager.PatchAsJsonAsync($"/api/v1/leads/{lead.Id}",
            new UpdateLeadRequest(null, contentId, null), TestJson.Options);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongManager.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/leads")).StatusCode);
    }

    [Fact]
    public async Task Audit_OnlyForAdmin_AndFiltersByEntity()
    {
        var admin = await factory.CreateAdminClientAsync();
        var manager = await factory.CreateManagerClientAsync();
        var customer = await BookingTests.CreateCustomerAsync(manager);

        var page = await admin.GetFromJsonAsync<PagedResult<AuditLogResponse>>(
            $"/api/v1/audit?entity=Customer&entityId={customer.Id}", TestJson.Options);
        var entry = Assert.Single(page!.Items);
        Assert.Equal("Created", entry.Action);
        Assert.Equal(SitoraApiFactory.ManagerEmail, entry.UserEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/audit")).StatusCode);
    }
}

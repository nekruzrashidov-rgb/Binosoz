using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Заявка застройщика с /register: подача без входа, рассмотрение создателем платформы.
public class OrgApplicationTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SubmitApplication_Valid_Returns200_SavesPending_AndSendsConfirmation()
    {
        var email = NewEmail();

        var response = await Submit(email, "98 363 70 70");

        var body = await TestScenarios.ReadAsync<SubmitApplicationResponse>(response);
        Assert.Equal("Заявка принята. Мы свяжемся с вами в ближайшее время.", body.Message);
        var saved = await factory.WithDbAsync(db => db.OrgApplications.SingleAsync(a => a.Id == body.ApplicationId));
        Assert.Equal((OrgApplicationStatus.Pending, email, "+992983637070", "ООО Тест Строй"), (saved.Status, saved.ContactEmail, saved.ContactPhone, saved.CompanyName));
        Assert.Equal(1, factory.Email.SentCount(email));
        var letter = factory.Email.LastMessage(email)!;
        Assert.Equal("Заявка принята — Binosoz", letter.Subject);
        Assert.Contains("<strong>ООО Тест Строй</strong>", letter.Html);
        Assert.Contains("ООО Тест Строй", letter.Text);
    }

    [Fact]
    public async Task ConfirmationLetter_EscapesWhatTheApplicantTyped()
    {
        var email = NewEmail();
        (await _client.PostAsJsonAsync("/api/v1/public/applications",
            new SubmitApplicationRequest("<b>Строй</b>", email, "+992983637070", "Иван <script>", null))).EnsureSuccessStatusCode();

        var html = factory.Email.LastMessage(email)!.Html!;
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;b&gt;Строй&lt;/b&gt;", html);
    }

    [Fact]
    public async Task SubmitApplication_DuplicateEmail_Within24h_Returns409()
    {
        var email = NewEmail();
        (await Submit(email)).EnsureSuccessStatusCode();

        var second = await Submit(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("уже на рассмотрении", await second.Content.ReadAsStringAsync());
        Assert.Equal(1, await factory.WithDbAsync(db => db.OrgApplications.CountAsync(a => a.ContactEmail == email)));
    }

    [Fact]
    public async Task SubmitApplication_InvalidPhone_Returns422_AndMalformedFields_400()
    {
        var badPhone = await Submit(NewEmail(), "12345");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badPhone.StatusCode);
        Assert.Contains("Неверный номер телефона", await badPhone.Content.ReadAsStringAsync());

        // Формат полей проверяет валидатор: как везде в API — 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit("not-an-email", "+992983637070")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/public/applications",
            new SubmitApplicationRequest("A", NewEmail(), "+992983637070", "Владелец", null))).StatusCode);
    }

    [Fact]
    public async Task PlatformOwner_CanApprove_CreatesOrganizationWithOwner()
    {
        var email = NewEmail();
        var id = await SubmitIdAsync(email);
        var platform = await factory.CreatePlatformOwnerClientAsync();
        var slug = "dev-" + Guid.NewGuid().ToString("N")[..8];

        var pending = await platform.GetFromJsonAsync<PagedResult<OrgApplicationResponse>>("/api/v1/platform/applications?status=Pending&pageSize=100", TestJson.Options);
        Assert.Contains(pending!.Items, a => a.Id == id);

        var approved = await TestScenarios.ReadAsync<ApproveApplicationResponse>(
            await platform.PostAsJsonAsync($"/api/v1/platform/applications/{id}/approve", new ApproveApplicationRequest(slug)));

        var organization = await factory.WithDbAsync(db => db.Organizations.SingleAsync(o => o.Id == approved.OrganizationId));
        Assert.Equal((slug, "ООО Тест Строй"), (organization.Slug, organization.Name));
        var application = await factory.WithDbAsync(db => db.OrgApplications.SingleAsync(a => a.Id == id));
        Assert.Equal((OrgApplicationStatus.Approved, organization.Id), (application.Status, application.OrganizationId));
        Assert.NotNull(application.ReviewedAt);
        Assert.NotNull(application.ReviewedByUserId);

        // Заявитель стал владельцем новой организации и получил письмо со входом.
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var owner = await users.FindByEmailAsync(email);
        Assert.Equal(organization.Id, owner!.OrganizationId);
        Assert.True(await users.IsInRoleAsync(owner, "Owner"));
        Assert.Equal(2, factory.Email.SentCount(email));
        // Письмо владельцу ведёт на страницу входа, а не на страницу приглашения без токена.
        Assert.EndsWith("/login", factory.Email.LastMessage(email)!.Text.TrimEnd());
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Entity == nameof(OrgApplication) && l.EntityId == id && l.Action == "Approved")));

        // Рассмотренную заявку повторно не одобрить.
        Assert.Equal(HttpStatusCode.Conflict, (await platform.PostAsJsonAsync($"/api/v1/platform/applications/{id}/approve",
            new ApproveApplicationRequest(slug + "-2"))).StatusCode);
    }

    [Fact]
    public async Task Approve_WhenMailIsDown_StillCreatesOrganization()
    {
        var email = NewEmail();
        var id = await SubmitIdAsync(email);
        var platform = await factory.CreatePlatformOwnerClientAsync();
        factory.Email.Failing[email] = true;

        var approved = await TestScenarios.ReadAsync<ApproveApplicationResponse>(
            await platform.PostAsJsonAsync($"/api/v1/platform/applications/{id}/approve",
                new ApproveApplicationRequest("dev-" + Guid.NewGuid().ToString("N")[..8])));

        var application = await factory.WithDbAsync(db => db.OrgApplications.SingleAsync(a => a.Id == id));
        Assert.Equal((OrgApplicationStatus.Approved, approved.OrganizationId), (application.Status, application.OrganizationId));
    }

    [Fact]
    public async Task PlatformOwner_CanReject_SetsRejected_AndTakenSlugKeepsPending()
    {
        var platform = await factory.CreatePlatformOwnerClientAsync();
        var id = await SubmitIdAsync(NewEmail());

        // Занятое короткое имя: организация не создаётся, заявка остаётся на рассмотрении.
        var taken = await platform.PostAsJsonAsync($"/api/v1/platform/applications/{id}/approve", new ApproveApplicationRequest("sitora"));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(OrgApplicationStatus.Pending, await factory.WithDbAsync(db => db.OrgApplications.Where(a => a.Id == id).Select(a => a.Status).SingleAsync()));

        Assert.Equal(HttpStatusCode.BadRequest, (await platform.PostAsJsonAsync($"/api/v1/platform/applications/{id}/reject",
            new RejectApplicationRequest(""))).StatusCode);
        var rejected = await platform.PostAsJsonAsync($"/api/v1/platform/applications/{id}/reject", new RejectApplicationRequest("Нет документов на землю"));

        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        var application = await factory.WithDbAsync(db => db.OrgApplications.SingleAsync(a => a.Id == id));
        Assert.Equal((OrgApplicationStatus.Rejected, "Нет документов на землю"), (application.Status, application.ReviewNote));
        var list = await platform.GetFromJsonAsync<PagedResult<OrgApplicationResponse>>("/api/v1/platform/applications?status=Rejected&pageSize=100", TestJson.Options);
        Assert.Contains(list!.Items, a => a.Id == id);
    }

    [Fact]
    public async Task OtherRoles_CannotSeeOrReviewApplications()
    {
        var id = await SubmitIdAsync(NewEmail());
        var admin = await factory.CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/platform/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/platform/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/v1/platform/applications/{id}/approve",
            new ApproveApplicationRequest("dev-" + Guid.NewGuid().ToString("N")[..8]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/v1/platform/applications/{id}/reject",
            new RejectApplicationRequest("нет"))).StatusCode);
    }

    private static string NewEmail() => $"owner-{Guid.NewGuid():N}@company.test";

    private Task<HttpResponseMessage> Submit(string email, string phone = "+992983637070") =>
        _client.PostAsJsonAsync("/api/v1/public/applications",
            new SubmitApplicationRequest("ООО Тест Строй", email, phone, "Тест Владелец", "Два дома в Душанбе"));

    private async Task<Guid> SubmitIdAsync(string email) =>
        (await TestScenarios.ReadAsync<SubmitApplicationResponse>(await Submit(email))).ApplicationId;
}

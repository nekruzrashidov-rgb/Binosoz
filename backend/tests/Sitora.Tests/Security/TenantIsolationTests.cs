using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Construction;
using Sitora.Domain.DTOs.Documents;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Данные застройщика A (организация по умолчанию из сидера) и застройщика B (создан через платформу)
// не пересекаются ни на одном адресе: чужое выглядит как несуществующее (404) или закрыто (403).
public class TenantIsolationTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private record TenantA(DealScenario Deal, Guid BuildingId, Guid FloorId, Guid ApartmentId, Guid LeadId,
        Guid PaymentId, Guid DocumentId, Guid StageId, Guid ManagerId);

    [Fact]
    public async Task OwnerB_SeesNothingOfOwnerA_OnEveryListAndByIdAddress()
    {
        var a = await CreateTenantADataAsync();
        var (ownerB, bBuilding, _) = await CreateOrganizationBAsync();
        var aIds = new[] { a.Deal.DealId, a.Deal.CustomerId, a.BuildingId, a.LeadId, a.ManagerId, a.PaymentId }
            .Select(id => id.ToString()).ToArray();

        // Списки: только своё (у B — один дом и сам владелец).
        string[] lists =
        [
            "/api/v1/deals", "/api/v1/customers", "/api/v1/leads", "/api/v1/users", "/api/v1/audit?pageSize=100",
            "/api/v1/buildings", "/api/v1/crm/summary", "/api/v1/crm/pipeline", "/api/v1/reports/payments",
            "/api/v1/reports/debts", "/api/v1/invitations"
        ];
        foreach (var url in lists)
        {
            var response = await ownerB.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode} {body}");
            foreach (var id in aIds)
                Assert.False(body.Contains(id, StringComparison.OrdinalIgnoreCase), $"{url} показал чужой id {id}");
        }

        var buildings = await ownerB.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options);
        Assert.Equal([bBuilding], buildings!.Items.Select(b => b.Id));
        Assert.Equal(0, (await ownerB.GetFromJsonAsync<PagedResult<DealResponse>>("/api/v1/deals", TestJson.Options))!.TotalCount);

        // Чтение по id.
        string[] reads =
        [
            $"/api/v1/deals/{a.Deal.DealId}", $"/api/v1/customers/{a.Deal.CustomerId}", $"/api/v1/buildings/{a.BuildingId}",
            $"/api/v1/buildings/{a.BuildingId}/floors", $"/api/v1/buildings/{a.BuildingId}/stages",
            $"/api/v1/apartments/{a.ApartmentId}", $"/api/v1/floors/{a.FloorId}/apartments",
            $"/api/v1/deals/{a.Deal.DealId}/schedule", $"/api/v1/deals/{a.Deal.DealId}/schedule.pdf",
            $"/api/v1/deals/{a.Deal.DealId}/statement.pdf", $"/api/v1/deals/{a.Deal.DealId}/documents",
            $"/api/v1/deals/{a.Deal.DealId}/messages", $"/api/v1/documents/{a.DocumentId}/download",
            $"/api/v1/deals?customerId={a.Deal.CustomerId}"
        ];
        foreach (var url in reads)
        {
            var response = await ownerB.GetAsync(url);
            if (url.Contains("?customerId="))
            {
                Assert.Equal(0, (await response.Content.ReadFromJsonAsync<PagedResult<DealResponse>>(TestJson.Options))!.TotalCount);
                continue;
            }
            AssertHidden(response, $"GET {url}");
        }

        // Изменения чужого.
        var stage = new SaveStageRequest("Чужой этап", 50, 1);
        (HttpMethod Method, string Url, object Body)[] writes =
        [
            (HttpMethod.Put, $"/api/v1/buildings/{a.BuildingId}", new SaveBuildingRequest("Захват", "Захват", "ул.", 5, null, null)),
            (HttpMethod.Post, "/api/v1/floors", new CreateFloorRequest(a.BuildingId, 99, null)),
            (HttpMethod.Put, $"/api/v1/floors/{a.FloorId}", new UpdateFloorRequest(98, null)),
            (HttpMethod.Post, "/api/v1/apartments", new CreateApartmentRequest(a.FloorId, 999, 1, 40m, 5000m, 7)),
            (HttpMethod.Put, $"/api/v1/apartments/{a.ApartmentId}", new UpdateApartmentRequest(1, 1, 1m, 1m, 0)),
            (HttpMethod.Patch, $"/api/v1/apartments/{a.ApartmentId}/on-request", new SetOnRequestRequest(true)),
            (HttpMethod.Post, $"/api/v1/apartments/{a.ApartmentId}/bookings", new CreateBookingRequest("Чужой", SitoraApiFactory.RandomPhone(), null, null)),
            (HttpMethod.Patch, $"/api/v1/deals/{a.Deal.DealId}/stage", new ChangeDealStageRequest(DealStage.Cancelled, "захват")),
            (HttpMethod.Post, $"/api/v1/deals/{a.Deal.DealId}/plan", new CreatePlanRequest(10, 5, 5, TestScenarios.Today)),
            (HttpMethod.Post, $"/api/v1/deals/{a.Deal.DealId}/payments", new CreatePaymentRequest(1m, Currency.TJS, TestScenarios.Today, PaymentMethod.Cash, null)),
            (HttpMethod.Post, $"/api/v1/payments/{a.PaymentId}/reverse", new ReversePaymentRequest("захват")),
            (HttpMethod.Post, $"/api/v1/deals/{a.Deal.DealId}/messages", new SendMessageRequest("привет")),
            (HttpMethod.Patch, $"/api/v1/leads/{a.LeadId}", new UpdateLeadRequest(LeadStatus.Rejected, null, "захват")),
            (HttpMethod.Patch, $"/api/v1/users/{a.ManagerId}/status", new SetUserStatusRequest(false)),
            (HttpMethod.Put, $"/api/v1/users/{a.ManagerId}/buildings", new SetUserBuildingsRequest([bBuilding])),
            (HttpMethod.Post, $"/api/v1/buildings/{a.BuildingId}/stages", stage),
            (HttpMethod.Put, $"/api/v1/stages/{a.StageId}", stage),
            (HttpMethod.Post, "/api/v1/deals", new CreateDealRequest(a.ApartmentId, a.Deal.CustomerId, DealStage.Contract, Currency.TJS, null, null)),
            (HttpMethod.Post, "/api/v1/invitations", new CreateInvitationRequest($"x-{Guid.NewGuid():N}@b.test", "Чужой", "ObjectAdmin", [a.BuildingId])),
            (HttpMethod.Post, "/api/v1/users", new CreateUserRequest($"y-{Guid.NewGuid():N}@b.test", "Чужой", "Manager", null, null, [a.BuildingId]))
        ];
        foreach (var (method, url, body) in writes)
        {
            using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, body.GetType(), options: TestJson.Options) };
            AssertHidden(await ownerB.SendAsync(request), $"{method} {url}");
        }

        var upload = new MultipartFormDataContent { { Pdf(), "file", "чужой.pdf" }, { new StringContent(nameof(DocumentType.Contract)), "type" } };
        AssertHidden(await ownerB.PostAsync($"/api/v1/deals/{a.Deal.DealId}/documents", upload), "POST documents");

        // У A ничего не поменялось.
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(DealStage.Contract, (await db.Deals.SingleAsync(d => d.Id == a.Deal.DealId)).Stage);
            Assert.Equal(1, await db.Payments.CountAsync(p => p.DealId == a.Deal.DealId));
            Assert.True((await db.Users.SingleAsync(u => u.Id == a.ManagerId)).IsActive);
            Assert.False(await db.UserBuildings.AnyAsync(ub => ub.UserId == a.ManagerId));
            Assert.Equal(LeadStatus.New, (await db.Leads.SingleAsync(l => l.Id == a.LeadId)).Status);
            Assert.False(await db.Floors.AnyAsync(f => f.BuildingId == a.BuildingId && f.Number == 99));
            Assert.NotEqual(ApartmentStatus.OnRequest, (await db.Apartments.SingleAsync(x => x.Id == a.ApartmentId)).Status);
        });
    }

    [Fact]
    public async Task OwnerA_DoesNotSeeOrganizationB_AndPublicSiteFiltersBySlug()
    {
        var (_, bBuilding, slug) = await CreateOrganizationBAsync();
        var ownerA = await factory.CreateAdminClientAsync();

        AssertHidden(await ownerA.GetAsync($"/api/v1/buildings/{bBuilding}"), "A читает дом B");
        AssertHidden(await ownerA.PutAsJsonAsync($"/api/v1/buildings/{bBuilding}",
            new SaveBuildingRequest("Захват", "Захват", "ул.", 5, null, null)), "A меняет дом B");
        var aBuildings = await ownerA.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?pageSize=100", TestJson.Options);
        Assert.DoesNotContain(bBuilding, aBuildings!.Items.Select(b => b.Id));

        // Владелец не видит раздел платформы.
        Assert.Equal(HttpStatusCode.Forbidden, (await ownerA.GetAsync("/api/v1/platform/organizations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ownerA.PostAsync($"/api/v1/platform/impersonate/{Organization.DefaultId}", null)).StatusCode);

        var guest = factory.CreateClient();
        var bPublic = await guest.GetFromJsonAsync<PagedResult<BuildingResponse>>($"/api/v1/buildings?organization={slug}", TestJson.Options);
        Assert.Equal([bBuilding], bPublic!.Items.Select(b => b.Id));
        var aPublic = await guest.GetFromJsonAsync<PagedResult<BuildingResponse>>($"/api/v1/buildings?organization={Organization.DefaultSlug}&pageSize=100", TestJson.Options);
        Assert.NotEmpty(aPublic!.Items);
        Assert.DoesNotContain(bBuilding, aPublic.Items.Select(b => b.Id));
    }

    [Fact]
    public async Task ObjectAdmin_SeesOnlyOwnBuilding_NotNeighbourOfSameOrganization()
    {
        var owner = await factory.CreateAdminClientAsync();
        var mine = await TestScenarios.CreateDealAsync(factory);
        var myApartment = await factory.CreateClient().GetFromJsonAsync<ApartmentResponse>($"/api/v1/apartments/{(await DealAsync(owner, mine.DealId)).ApartmentId}", TestJson.Options);
        var myBuilding = myApartment!.BuildingId;

        // Соседний дом той же организации со своей сделкой.
        var neighbour = await TestScenarios.ReadAsync<BuildingResponse>(await owner.PostAsJsonAsync("/api/v1/buildings",
            new SaveBuildingRequest("Соседний", "Ҳамсоя", "ул. Соседняя, 2", 9, null, null)));
        var floor = await TestScenarios.ReadAsync<FloorResponse>(await owner.PostAsJsonAsync("/api/v1/floors", new CreateFloorRequest(neighbour.Id, 2, null)));
        var apartment = await TestScenarios.ReadAsync<ApartmentResponse>(await owner.PostAsJsonAsync("/api/v1/apartments",
            new CreateApartmentRequest(floor.Id, 21, 2, 60m, 7000m, 0)));
        var neighbourCustomer = await TestScenarios.ReadAsync<CustomerResponse>(await owner.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Сосед", SitoraApiFactory.RandomPhone(), null, null, null, neighbour.Id)));
        var neighbourDeal = await TestScenarios.ReadAsync<DealResponse>(await owner.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(apartment.Id, neighbourCustomer.Id, DealStage.Contract, Currency.TJS, null, null), TestJson.Options));

        // Админ объекта на первый дом: заводится владельцем, входит по коду из почты.
        var email = $"object-{Guid.NewGuid():N}@sitora.local";
        var created = await TestScenarios.ReadAsync<UserResponse>(await owner.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(email, "Админ объекта", "ObjectAdmin", null, null, [myBuilding])));
        Assert.Equal([myBuilding], created.BuildingIds);
        var objectAdmin = await factory.CreateEmailClientAsync(email);

        var me = await objectAdmin.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options);
        Assert.Equal(("ObjectAdmin", Organization.DefaultId), (me!.Role, me.OrganizationId));
        Assert.Equal([myBuilding], me.BuildingIds);

        var buildings = await objectAdmin.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options);
        Assert.Equal([myBuilding], buildings!.Items.Select(b => b.Id));
        var deals = await objectAdmin.GetFromJsonAsync<PagedResult<DealResponse>>("/api/v1/deals?pageSize=100", TestJson.Options);
        Assert.Contains(mine.DealId, deals!.Items.Select(d => d.Id));
        Assert.DoesNotContain(neighbourDeal.Id, deals.Items.Select(d => d.Id));
        var customers = await objectAdmin.GetFromJsonAsync<PagedResult<CustomerListItem>>("/api/v1/customers?pageSize=100", TestJson.Options);
        Assert.DoesNotContain(neighbourCustomer.Id, customers!.Items.Select(c => c.Id));
        Assert.Equal(HttpStatusCode.OK, (await objectAdmin.GetAsync($"/api/v1/deals/{mine.DealId}")).StatusCode);

        AssertHidden(await objectAdmin.GetAsync($"/api/v1/buildings/{neighbour.Id}"), "дом соседа");
        AssertHidden(await objectAdmin.GetAsync($"/api/v1/deals/{neighbourDeal.Id}"), "сделка соседа");
        AssertHidden(await objectAdmin.GetAsync($"/api/v1/customers/{neighbourCustomer.Id}"), "клиент соседа");
        AssertHidden(await objectAdmin.GetAsync($"/api/v1/apartments/{apartment.Id}"), "квартира соседа");
        AssertHidden(await objectAdmin.PutAsJsonAsync($"/api/v1/buildings/{neighbour.Id}",
            new SaveBuildingRequest("Захват", "Захват", "ул.", 5, null, null)), "правка дома соседа");
        AssertHidden(await objectAdmin.PatchAsJsonAsync($"/api/v1/deals/{neighbourDeal.Id}/stage",
            new ChangeDealStageRequest(DealStage.Cancelled, "захват"), TestJson.Options), "этап сделки соседа");

        // Админ объекта не создаёт дома, не трогает руководителей и не приглашает в чужой дом.
        Assert.Equal(HttpStatusCode.Forbidden, (await objectAdmin.PostAsJsonAsync("/api/v1/buildings",
            new SaveBuildingRequest("Новый", "Нав", "ул.", 3, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await objectAdmin.GetAsync("/api/v1/audit")).StatusCode);
        AssertHidden(await objectAdmin.PostAsJsonAsync("/api/v1/invitations",
            new CreateInvitationRequest($"m-{Guid.NewGuid():N}@sitora.local", "Менеджер", "Manager", [neighbour.Id])), "приглашение в чужой дом");
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == SitoraApiFactory.AdminEmail).Select(u => u.Id).SingleAsync());
        AssertHidden(await objectAdmin.PatchAsJsonAsync($"/api/v1/users/{adminId}/status", new SetUserStatusRequest(false)), "блокировка владельца");
        var ownInvite = await objectAdmin.PostAsJsonAsync("/api/v1/invitations",
            new CreateInvitationRequest($"m-{Guid.NewGuid():N}@sitora.local", "Менеджер", "Manager", null));
        Assert.Equal(HttpStatusCode.Created, ownInvite.StatusCode);
        Assert.Equal([myBuilding], (await ownInvite.Content.ReadFromJsonAsync<InvitationResponse>(TestJson.Options))!.BuildingIds);
    }

    [Fact]
    public async Task Invitation_CreatesStaffInBuilding_OnceOnly()
    {
        var owner = await factory.CreateAdminClientAsync();
        var building = (await owner.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options))!.Items[0].Id;
        var email = $"Invited-{Guid.NewGuid():N}@Sitora.Local ";
        var normalized = email.Trim().ToLowerInvariant();

        // Админу объекта нужен ровно один дом.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync("/api/v1/invitations",
            new CreateInvitationRequest(email, "Без дома", "ObjectAdmin", null))).StatusCode);
        // Владельцев заводит только создатель платформы.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync("/api/v1/invitations",
            new CreateInvitationRequest(email, "Второй владелец", "Owner", null))).StatusCode);

        var invitation = await TestScenarios.ReadAsync<InvitationResponse>(await owner.PostAsJsonAsync("/api/v1/invitations",
            new CreateInvitationRequest(email, "Приглашённый админ", "ObjectAdmin", [building])));
        Assert.Equal(normalized, invitation.Email);
        var token = factory.Email.LastInviteToken(normalized);
        Assert.NotNull(token);

        var guest = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/auth/invitations/accept",
            new AcceptInvitationRequest(new string('A', 64)))).StatusCode);
        var auth = await TestScenarios.ReadAsync<AuthResponse>(await guest.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(token)));
        Assert.Equal(("ObjectAdmin", Organization.DefaultId), (auth.User.Role, auth.User.OrganizationId));
        Assert.Equal([building], auth.User.BuildingIds);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/auth/invitations/accept",
            new AcceptInvitationRequest(token))).StatusCode);

        // Дальше — обычный вход по коду из почты.
        var again = await factory.CreateEmailClientAsync(normalized);
        Assert.Equal("ObjectAdmin", (await again.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options))!.Role);
        Assert.DoesNotContain((await owner.GetFromJsonAsync<List<InvitationResponse>>("/api/v1/invitations", TestJson.Options))!,
            i => i.Id == invitation.Id);
    }

    private static void AssertHidden(HttpResponseMessage response, string what) =>
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"{what}: {(int)response.StatusCode} {response.Content.ReadAsStringAsync().Result}");

    private static async Task<DealResponse> DealAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<DealResponse>($"/api/v1/deals/{id}", TestJson.Options))!;

    private async Task<TenantA> CreateTenantADataAsync()
    {
        var owner = await factory.CreateAdminClientAsync();
        var manager = await factory.CreateManagerClientAsync();
        var accountant = await factory.CreateAccountantClientAsync();
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var payment = await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, 100m));
        var apartmentId = (await DealAsync(owner, deal.DealId)).ApartmentId;
        var apartment = (await factory.CreateClient().GetFromJsonAsync<ApartmentResponse>($"/api/v1/apartments/{apartmentId}", TestJson.Options))!;
        var file = Pdf();
        var document = await TestScenarios.ReadAsync<DocumentResponse>(await manager.PostAsync($"/api/v1/deals/{deal.DealId}/documents",
            new MultipartFormDataContent { { file, "file", "договор.pdf" }, { new StringContent(nameof(DocumentType.Contract)), "type" } }));
        (await manager.PostAsJsonAsync($"/api/v1/deals/{deal.DealId}/messages", new SendMessageRequest("сообщение A"))).EnsureSuccessStatusCode();
        var lead = await TestScenarios.ReadAsync<LeadResponse>(await factory.CreateClient().PostAsJsonAsync("/api/v1/leads",
            new CreateLeadRequest("Заявка A", SitoraApiFactory.RandomPhone(), null, LeadSource.Website, null, apartment.BuildingId), TestJson.Options));
        var stage = await TestScenarios.ReadAsync<StageResponse>(await owner.PostAsJsonAsync($"/api/v1/buildings/{apartment.BuildingId}/stages",
            new SaveStageRequest("Фундамент", 100, 1)));
        var managerId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == SitoraApiFactory.ManagerEmail).Select(u => u.Id).SingleAsync());
        return new TenantA(deal, apartment.BuildingId, apartment.FloorId, apartmentId, lead.Id, payment.Id, document.Id, stage.Id, managerId);
    }

    // Второй застройщик: создатель платформы заводит организацию, её владелец входит по почте и строит дом.
    private async Task<(HttpClient Owner, Guid BuildingId, string Slug)> CreateOrganizationBAsync()
    {
        var platform = await factory.CreatePlatformOwnerClientAsync();
        var slug = $"b-{Guid.NewGuid():N}"[..20];
        var ownerEmail = $"owner-{slug}@b.test";
        await TestScenarios.ReadAsync<OrganizationResponse>(await platform.PostAsJsonAsync("/api/v1/platform/organizations",
            new CreateOrganizationRequest("Застройщик B", slug, ownerEmail, "Владелец B")));
        var owner = await factory.CreateEmailClientAsync(ownerEmail);
        var building = await TestScenarios.ReadAsync<BuildingResponse>(await owner.PostAsJsonAsync("/api/v1/buildings",
            new SaveBuildingRequest("Дом B", "Хонаи B", "ул. Второго, 1", 5, null, null)));
        return (owner, building.Id, slug);
    }

    private static ByteArrayContent Pdf()
    {
        var file = new ByteArrayContent("%PDF-1.4 тест"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return file;
    }
}

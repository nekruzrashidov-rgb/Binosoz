using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.WebApi.Middleware;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

public class ImpersonationTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Impersonation_IsMarkedInTokenAndResponses_AndEveryActionIsAudited()
    {
        var platform = await factory.CreatePlatformOwnerClientAsync();
        var platformUserId = await factory.WithDbAsync(db =>
            db.Users.Where(u => u.Email == SitoraApiFactory.PlatformOwnerEmail).Select(u => u.Id).SingleAsync());

        var organizations = await platform.GetFromJsonAsync<List<OrganizationResponse>>("/api/v1/platform/organizations", TestJson.Options);
        Assert.Contains(organizations!, o => o.Id == Organization.DefaultId);

        // Без режима выходить не из чего.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsync("/api/v1/platform/impersonate/stop", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await platform.PostAsync($"/api/v1/platform/impersonate/{Guid.NewGuid()}", null)).StatusCode);

        var started = await TestScenarios.ReadAsync<AccessTokenResponse>(
            await platform.PostAsync($"/api/v1/platform/impersonate/{Organization.DefaultId}", null));
        Assert.Equal(new ImpersonationInfo(platformUserId, Organization.DefaultId), started.User.Impersonation);
        Assert.Equal(("Owner", Organization.DefaultId), (started.User.Role, started.User.OrganizationId));
        var jwt = new JsonWebToken(started.AccessToken);
        Assert.Equal(platformUserId.ToString(), jwt.GetClaim("imp").Value);

        var impersonated = factory.CreateClient();
        impersonated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", started.AccessToken);

        // Видит данные организации и в каждом ответе — пометка.
        var buildings = await impersonated.GetAsync("/api/v1/deals");
        Assert.Equal(HttpStatusCode.OK, buildings.StatusCode);
        Assert.Equal(platformUserId.ToString(), buildings.Headers.GetValues(ImpersonationAuditMiddleware.Header).Single());
        var me = await impersonated.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options);
        Assert.NotNull(me!.Impersonation);

        // Изменение в режиме: и запись сервиса, и запись режима — с настоящим автором.
        var building = await TestScenarios.ReadAsync<BuildingResponse>(await impersonated.PostAsJsonAsync("/api/v1/buildings",
            new SaveBuildingRequest("Дом от платформы", "Хона", "ул. Платформы, 1", 4, null, null)));
        // Неудачная попытка тоже записывается.
        Assert.Equal(HttpStatusCode.NotFound, (await impersonated.PutAsJsonAsync($"/api/v1/buildings/{Guid.NewGuid()}",
            new SaveBuildingRequest("Нет", "Нет", "ул.", 1, null, null))).StatusCode);

        var stopped = await TestScenarios.ReadAsync<AccessTokenResponse>(await impersonated.PostAsync("/api/v1/platform/impersonate/stop", null));
        Assert.Null(stopped.User.Impersonation);
        Assert.Equal("PlatformOwner", stopped.User.Role);

        await factory.WithDbAsync(async db =>
        {
            var rows = await db.AuditLogs.IgnoreQueryFilters().Where(a => a.UserId == platformUserId).ToListAsync();
            Assert.Contains(rows, a => a.Action == "ImpersonationStarted" && a.EntityId == Organization.DefaultId);
            Assert.Contains(rows, a => a.Action == "ImpersonationStopped" && a.EntityId == Organization.DefaultId);
            var created = Assert.Single(rows, a => a.Entity == nameof(Building) && a.EntityId == building.Id);
            Assert.Equal((platformUserId, Organization.DefaultId), (created.ImpersonatedById, created.OrganizationId));
            Assert.Contains(rows, a => a.Entity == "Impersonation" && a.Action == "POST /api/v1/buildings"
                                       && a.ImpersonatedById == platformUserId && a.After!.Contains("201"));
            Assert.Contains(rows, a => a.Entity == "Impersonation" && a.Action.StartsWith("PUT /api/v1/buildings/")
                                       && a.After!.Contains("404"));
        });

        // Владелец организации видит эти записи в своём аудите.
        var owner = await factory.CreateAdminClientAsync();
        var audit = await owner.GetFromJsonAsync<PagedResult<AuditLogResponse>>(
            $"/api/v1/audit?entity={nameof(Building)}&entityId={building.Id}", TestJson.Options);
        Assert.Equal(platformUserId, Assert.Single(audit!.Items).ImpersonatedById);

        // Токен режима не обновляется: refresh-токена у него нет, и платформа после выхода снова без организации.
        var platformAgain = factory.CreateClient();
        platformAgain.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stopped.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await platformAgain.GetAsync("/api/v1/platform/organizations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await impersonated.GetAsync("/api/v1/platform/organizations")).StatusCode);
    }
}

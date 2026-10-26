using System.Net;
using System.Net.Http.Json;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

public class UserTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Admin_CreatesStaffUser_WhoCanLogIn()
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"manager2-{Guid.NewGuid():N}@sitora.local";
        const string password = "Second-Manager-1";

        var created = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Второй менеджер", "Manager", password, null));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var users = await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/v1/users?pageSize=100", TestJson.Options);
        var user = Assert.Single(users!.Items, u => u.Email == email);
        Assert.Equal(["Manager"], user.Roles);
    }

    [Fact]
    public async Task CreateUser_Validates_InRussian()
    {
        var admin = await factory.CreateAdminClientAsync();

        var weak = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest($"w-{Guid.NewGuid():N}@sitora.local", "Слабый", "Content", "abc", null));
        var duplicate = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(SitoraApiFactory.ManagerEmail, "Дубль", "Manager", "Strong-Pass-1", null));
        var customerRole = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest($"c-{Guid.NewGuid():N}@sitora.local", "Покупатель", "Customer", "Strong-Pass-1", null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        Assert.Contains("не короче 8 символов", await weak.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, customerRole.StatusCode);
    }

    [Fact]
    public async Task Users_OnlyForAdmin()
    {
        var manager = await factory.CreateManagerClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/users")).StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Auth;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Своя база: тесты блокируют учётные записи.
public class LockoutTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private const string Invalid = "Неверная почта или пароль";
    private const string Locked = "Слишком много неудачных попыток входа, попробуйте позже";

    [Fact]
    public async Task FiveWrongPasswords_LockAccountEvenForCorrectPassword_UntilAdminUnblocks()
    {
        var (userId, email, password) = await CreateStaffAsync();
        var guest = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            Assert.Equal(Invalid, await LoginDetailAsync(guest, email, "Wrong-password-1"));

        // Даже верный пароль не пускает, пока действует блокировка.
        Assert.Equal(Locked, await LoginDetailAsync(guest, email, password));

        var admin = await factory.CreateAdminClientAsync();
        (await admin.PatchAsJsonAsync($"/api/v1/users/{userId}/status", new SetUserStatusRequest(true))).EnsureSuccessStatusCode();

        var login = await guest.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task UnknownEmail_GetsSameAnswersAsRealAccount()
    {
        var guest = factory.CreateClient();
        var unknown = $"nobody-{Guid.NewGuid():N}@sitora.local";

        for (var i = 0; i < 5; i++)
            Assert.Equal(Invalid, await LoginDetailAsync(guest, unknown, "Wrong-password-1"));

        // Как у настоящей учётной записи после пяти ошибок — по ответу не понять, есть ли такая почта.
        Assert.Equal(Locked, await LoginDetailAsync(guest, unknown, "Wrong-password-1"));
        Assert.Equal(Locked, await LoginDetailAsync(guest, unknown.ToUpperInvariant(), "Wrong-password-1"));
    }

    [Fact]
    public async Task BlockedAccount_AnswersLikeUnknownEmail()
    {
        var (userId, email, password) = await CreateStaffAsync();
        var admin = await factory.CreateAdminClientAsync();
        (await admin.PatchAsJsonAsync($"/api/v1/users/{userId}/status", new SetUserStatusRequest(false))).EnsureSuccessStatusCode();

        Assert.Equal(Invalid, await LoginDetailAsync(factory.CreateClient(), email, password));
    }

    private async Task<(Guid Id, string Email, string Password)> CreateStaffAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"lockout-{Guid.NewGuid():N}@sitora.local";
        const string password = "Lockout-Test-1";
        var user = await TestScenarios.ReadAsync<UserResponse>(await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(email, "Проверка блокировки", "Manager", password, null)));
        return (user.Id, email, password);
    }

    private static async Task<string?> LoginDetailAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;
    }
}

using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// C-06 / R-04: журнал CRM и «Активность платформы» отдают понятного автора и объект: вход — от имени самого
// пользователя (а не «система»), объект — ФИО или номер квартиры, «система» — только сидер и фоновые задачи.
public class AuditJournalTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Login_IsByTheUserHimself_WithNameAndEmail()
    {
        var owner = await factory.CreateAdminClientAsync();
        var (adminId, fullName) = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == SitoraApiFactory.AdminEmail)
            .Select(u => new ValueTuple<Guid, string>(u.Id, u.FullName)).SingleAsync());

        var page = (await owner.GetFromJsonAsync<PagedResult<AuditLogResponse>>(
            $"/api/v1/audit?entity={nameof(AppUser)}&entityId={adminId}", TestJson.Options))!;
        var login = page.Items.First(a => a.Action == "Login");
        Assert.Equal(adminId, login.UserId);
        Assert.Equal(SitoraApiFactory.AdminEmail, login.UserEmail);
        Assert.Equal(fullName, login.UserName);
        Assert.False(login.BySystem);
        Assert.Equal(fullName, login.EntityName);
    }

    [Fact]
    public async Task SeederAction_IsBySystem_AndBookingShowsApartmentNumber()
    {
        var owner = await factory.CreateAdminClientAsync();
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == SitoraApiFactory.AdminEmail).Select(u => u.Id).SingleAsync());
        await factory.WithDbAsync(async db =>
        {
            db.AuditLogs.Add(new AuditLog
            {
                Entity = nameof(AppUser), EntityId = adminId, Action = SeedPasswords.PasswordSetAction,
                OrganizationId = Organization.DefaultId, At = DateTime.UtcNow, After = "{\"Secret\":\"X\"}"
            });
            await db.SaveChangesAsync();
        });
        var seeded = (await owner.GetFromJsonAsync<PagedResult<AuditLogResponse>>(
            $"/api/v1/audit?entity={nameof(AppUser)}&entityId={adminId}", TestJson.Options))!.Items
            .First(a => a.Action == SeedPasswords.PasswordSetAction);
        Assert.True(seeded.BySystem);
        Assert.Null(seeded.UserEmail);

        // Бронь гостя: объект — номер квартиры и имя из заявки.
        var apartmentId = await factory.FindFreeApartmentAsync();
        var number = await factory.WithDbAsync(db => db.Apartments.Where(a => a.Id == apartmentId).Select(a => a.Number).SingleAsync());
        var booked = await factory.CreateClient().PostAsJsonAsync($"/api/v1/apartments/{apartmentId}/bookings",
            new CreateBookingRequest("Журнал Тест", SitoraApiFactory.RandomPhone(), null, null));
        booked.EnsureSuccessStatusCode();
        var booking = (await booked.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        var entry = (await owner.GetFromJsonAsync<PagedResult<AuditLogResponse>>(
            $"/api/v1/audit?entity={nameof(Booking)}&entityId={booking.Id}", TestJson.Options))!.Items.First();
        Assert.Equal(number, entry.ApartmentNumber);
        Assert.Equal("Журнал Тест", entry.EntityName);
    }

    [Fact]
    public async Task PlatformActivity_HasAuthorName_ObjectName_AndChange()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var page = (await platform.GetFromJsonAsync<PagedResult<PlatformActivityItem>>(
            "/api/v1/platform/activity?action=Login&pageSize=100", TestJson.Options))!;
        var login = page.Items.First(i => i.UserEmail == SitoraApiFactory.SuperAdminEmail);
        Assert.False(login.BySystem);
        Assert.False(string.IsNullOrWhiteSpace(login.UserName));
        Assert.Contains("Password", login.After ?? "");
    }
}

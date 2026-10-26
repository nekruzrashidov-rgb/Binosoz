using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

public class CustomerTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task PassportData_IsEncryptedInDatabase_AndReturnedOnlyInDetails()
    {
        var manager = await factory.CreateManagerClientAsync();
        var phone = SitoraApiFactory.RandomPhone();

        var created = await BookingTests.CreateCustomerAsync(manager, phone);
        Assert.Equal("A7654321", created.PassportData);

        var stored = await factory.WithDbAsync(db => db.Customers.Where(c => c.Id == created.Id)
            .Select(c => c.PassportDataEncrypted).SingleAsync());
        Assert.StartsWith("v1:", stored);
        Assert.DoesNotContain("A7654321", stored);

        var details = await manager.GetFromJsonAsync<CustomerResponse>($"/api/v1/customers/{created.Id}", TestJson.Options);
        Assert.Equal("A7654321", details!.PassportData);

        var list = await manager.GetStringAsync($"/api/v1/customers?search={Uri.EscapeDataString(phone[^7..])}");
        Assert.Contains(created.Id.ToString(), list);
        Assert.DoesNotContain("A7654321", list);

        var audit = await factory.WithDbAsync(db => db.AuditLogs.SingleAsync(a => a.EntityId == created.Id));
        Assert.DoesNotContain("A7654321", audit.After);
    }

    [Fact]
    public async Task DuplicatePhone_Returns409()
    {
        var manager = await factory.CreateManagerClientAsync();
        var phone = SitoraApiFactory.RandomPhone();
        await BookingTests.CreateCustomerAsync(manager, phone);

        var duplicate = await manager.PostAsJsonAsync("/api/v1/customers",
            new CreateCustomerRequest("Другой", phone, null, null, null));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Customers_OnlyForManagerAndAdmin()
    {
        var guest = await factory.CreateClient().GetAsync("/api/v1/customers");
        var content = await (await factory.CreateContentClientAsync()).GetAsync("/api/v1/customers");
        var accountant = await (await factory.CreateAccountantClientAsync()).GetAsync("/api/v1/customers");
        var admin = await (await factory.CreateAdminClientAsync()).GetAsync("/api/v1/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, content.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, accountant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task Seeder_CreatesTenCustomersAndFiveDealsOnDifferentStages()
    {
        var admin = await factory.CreateAdminClientAsync();

        var customers = await admin.GetFromJsonAsync<PagedResult<CustomerListItem>>("/api/v1/customers?search=example", TestJson.Options);
        var seededCustomers = await factory.WithDbAsync(db => db.Customers.CountAsync(c => c.Email!.EndsWith("@example.com")));
        var stages = await factory.WithDbAsync(db => db.Deals.Select(d => d.Stage).Distinct().CountAsync());

        Assert.NotNull(customers);
        Assert.Equal(10, seededCustomers);
        Assert.Equal(5, stages);
    }
}

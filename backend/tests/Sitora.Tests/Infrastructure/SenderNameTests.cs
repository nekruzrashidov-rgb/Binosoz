using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Tests.Infrastructure;

// Почта для реального застройщика: отправитель и подпись — имя организации, а не «Binosoz».
public class SenderNameTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static EmailOptions Options() => new() { From = "no-reply@nekruz.test", FromName = "Binosoz" };

    private static string FromLine(string mime) =>
        string.Join(" ", mime.Split("\r\n").SkipWhile(l => !l.StartsWith("From: ")).TakeWhile((l, i) => i == 0 || l.StartsWith(' ')));

    [Fact]
    public void Gmail_FromIsOrganization_WhenMessageHasIt()
    {
        var withBrand = GmailEmailSender.BuildMime(Options(), new EmailMessage("a@example.test", "Тема", "Текст", null, "Rich Tower"), DateTimeOffset.UtcNow);
        var expected = GmailEmailSender.BuildMime(new EmailOptions { From = "no-reply@nekruz.test", FromName = "Rich Tower" },
            new EmailMessage("a@example.test", "Тема", "Текст"), DateTimeOffset.UtcNow);
        var platform = GmailEmailSender.BuildMime(Options(), new EmailMessage("a@example.test", "Тема", "Текст"), DateTimeOffset.UtcNow);

        Assert.Equal(FromLine(expected), FromLine(withBrand));
        Assert.Contains("Binosoz", FromLine(platform));
        Assert.Contains("<no-reply@nekruz.test>", FromLine(withBrand));
    }

    [Fact]
    public void SenderName_NoHeaderInjection()
    {
        var name = EmailOptions.SenderName(Options(), new EmailMessage("a@example.test", "s", "t", null, "Застройщик\r\nBcc: x@evil.test"));
        Assert.DoesNotContain("\n", name);
        Assert.DoesNotContain("\r", name);
    }

    [Fact]
    public async Task CustomerCodeLetter_ComesFromTheDeveloper()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await factory.CreateCustomerClientAsync(deal.Phone);
        var (email, organization) = await factory.WithDbAsync(async db =>
        {
            var c = await db.Customers.IgnoreQueryFilters().Where(x => x.Id == deal.CustomerId).Select(x => new { x.Email, x.OrganizationId }).SingleAsync();
            return (c.Email!, await db.Organizations.IgnoreQueryFilters().Where(o => o.Id == c.OrganizationId).Select(o => o.Name).SingleAsync());
        });

        await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/customer/pin/reset-request", new CustomerEmailRequest(email));

        var letter = factory.Email.LastMessage(email)!;
        Assert.Equal(organization, letter.FromName);
        Assert.StartsWith(organization + ":", letter.Subject);
    }
}

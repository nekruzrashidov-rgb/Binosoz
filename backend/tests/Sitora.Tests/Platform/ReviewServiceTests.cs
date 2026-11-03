using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Reviews;
using Sitora.Tests.Ai;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Platform;

// Отзывы: только покупатель со сделкой, один на дом, правка до публикации, ответы застройщика и AI, удаление суперадмином.
// Фабрика с ключом Claude и подменённой моделью — как в тестах AI-помощника.
public class ReviewServiceTests(AiFactory factory) : IClassFixture<AiFactory>
{
    [Fact]
    public async Task Customer_WithDeal_Reviews_Once_NotVisibleUntilPublished()
    {
        var (customer, building) = await CustomerWithDealAsync();

        var review = await TestScenarios.ReadAsync<ReviewResponse>(await customer.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews",
            new SaveReviewRequest(5, "  Отличный дом, всё по договору  ")));
        Assert.Equal((5, "Отличный дом, всё по договору", false), (review.Rating, review.Text, review.IsPublished));
        Assert.DoesNotContain(await Public(building), r => r.Id == review.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews", new SaveReviewRequest(4, "Ещё раз"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await customer.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews", new SaveReviewRequest(6, "Шесть"))).StatusCode);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == review.Id && l.Action == "Created")));
    }

    [Fact]
    public async Task CustomerWithoutDealInBuilding_GetsForbidden_StaffCannotReviewAsCustomer()
    {
        var (customer, building) = await CustomerWithDealAsync();
        // Другой дом той же организации, где у покупателя сделки нет.
        var foreign = (await TestScenarios.ReadAsync<Sitora.Domain.DTOs.Catalog.BuildingResponse>(await (await factory.CreateAdminClientAsync())
            .PostAsJsonAsync("/api/v1/buildings", new Sitora.Domain.DTOs.Catalog.SaveBuildingRequest("Без сделок", "Бе аҳд", "ул. 3", 5, null, null)))).Id;

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"/api/v1/buildings/{foreign}/reviews", new SaveReviewRequest(3, "Чужой дом"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.PostAsJsonAsync($"/api/v1/buildings/{Guid.NewGuid()}/reviews", new SaveReviewRequest(3, "Нет дома"))).StatusCode);

        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews", new SaveReviewRequest(5, "Сам себя"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync($"/api/v1/buildings/{building}/reviews", new SaveReviewRequest(5, "Гость"))).StatusCode);
    }

    [Fact]
    public async Task Staff_Replies_Publishes_Hides_PublicSeesOnlyPublishedWithFirstName()
    {
        var (customer, building) = await CustomerWithDealAsync();
        var review = await TestScenarios.ReadAsync<ReviewResponse>(await customer.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews", new SaveReviewRequest(4, "Хорошо, но долго")));
        var manager = await factory.CreateManagerClientAsync();

        var all = await manager.GetFromJsonAsync<List<ReviewResponse>>($"/api/v1/buildings/{building}/reviews/all", TestJson.Options);
        Assert.Contains(all!, r => r.Id == review.Id && r.IsPublished == false && r.CustomerFullName != null);

        var replied = await TestScenarios.ReadAsync<ReviewResponse>(await manager.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews/{review.Id}/reply",
            new ReplyReviewRequest("Спасибо, ускоряем работы")));
        Assert.Equal("Спасибо, ускоряем работы", replied.OwnerReply);
        Assert.NotNull(replied.RepliedAt);

        (await manager.PostAsync($"/api/v1/buildings/{building}/reviews/{review.Id}/publish", null)).EnsureSuccessStatusCode();
        var shown = Assert.Single(await Public(building), r => r.Id == review.Id);
        Assert.Equal("Спасибо, ускоряем работы", shown.OwnerReply);
        Assert.Null(shown.AiReply);
        Assert.Null(shown.CustomerFullName);
        Assert.DoesNotContain(" ", shown.AuthorName);

        (await manager.PostAsync($"/api/v1/buildings/{building}/reviews/{review.Id}/hide", null)).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await Public(building), r => r.Id == review.Id);
        var actions = await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().Where(l => l.EntityId == review.Id).Select(l => l.Action).ToListAsync());
        Assert.Contains("Published", actions);
        Assert.Contains("Hidden", actions);
    }

    [Fact]
    public async Task Customer_EditsOnlyBeforePublishing()
    {
        var (customer, building) = await CustomerWithDealAsync();
        var review = await TestScenarios.ReadAsync<ReviewResponse>(await customer.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews", new SaveReviewRequest(2, "Плохо")));

        var edited = await TestScenarios.ReadAsync<ReviewResponse>(await customer.PutAsJsonAsync($"/api/v1/buildings/{building}/reviews/{review.Id}", new SaveReviewRequest(3, "Уже лучше")));
        Assert.Equal((3, "Уже лучше"), (edited.Rating, edited.Text));

        // Чужой отзыв не виден — 404.
        var (other, _) = await CustomerWithDealAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/buildings/{building}/reviews/{review.Id}", new SaveReviewRequest(1, "Захват"))).StatusCode);

        (await (await factory.CreateManagerClientAsync()).PostAsync($"/api/v1/buildings/{building}/reviews/{review.Id}/publish", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await customer.PutAsJsonAsync($"/api/v1/buildings/{building}/reviews/{review.Id}", new SaveReviewRequest(5, "После публикации"))).StatusCode);
    }

    [Fact]
    public async Task AiReply_UsesPromptWithTextAndRating_SavedButNotPublished()
    {
        var (customer, building) = await CustomerWithDealAsync();
        var review = await TestScenarios.ReadAsync<ReviewResponse>(await customer.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews", new SaveReviewRequest(1, "Хона хеле хунук аст")));
        var manager = await factory.CreateManagerClientAsync();

        var withAi = await TestScenarios.ReadAsync<ReviewResponse>(await manager.PostAsync($"/api/v1/buildings/{building}/reviews/{review.Id}/ai-reply", null));

        Assert.Equal("ответ модели", withAi.AiReply);
        Assert.Null(withAi.OwnerReply);
        Assert.False(withAi.IsPublished);
        Assert.Contains("менеджер застройщика в Таджикистане", factory.Model.LastSystem);
        Assert.Contains("Максимум 3 предложения", factory.Model.LastSystem);
        var prompt = Assert.Single(factory.Model.LastMessages).Content;
        Assert.Contains("Хона хеле хунук аст", prompt);
        Assert.Contains("1/5", prompt);
    }

    [Fact]
    public async Task SuperAdmin_ListsAndDeletes_OthersCannot()
    {
        var (customer, building) = await CustomerWithDealAsync();
        var review = await TestScenarios.ReadAsync<ReviewResponse>(await customer.PostAsJsonAsync($"/api/v1/buildings/{building}/reviews", new SaveReviewRequest(5, "Удалить меня")));
        var platform = await factory.CreateSuperAdminClientAsync();

        var list = await platform.GetFromJsonAsync<PagedResult<ReviewResponse>>("/api/v1/platform/reviews?pageSize=100", TestJson.Options);
        var row = Assert.Single(list!.Items, r => r.Id == review.Id);
        Assert.False(string.IsNullOrEmpty(row.BuildingName));
        Assert.False(string.IsNullOrEmpty(row.OrganizationName));

        var owner = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.DeleteAsync($"/api/v1/platform/reviews/{review.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await platform.DeleteAsync($"/api/v1/platform/reviews/{review.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await platform.DeleteAsync($"/api/v1/platform/reviews/{review.Id}")).StatusCode);
        Assert.False(await factory.WithDbAsync(db => db.Reviews.IgnoreQueryFilters().AnyAsync(r => r.Id == review.Id)));
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == review.Id && l.Action == "Deleted")));
    }

    private async Task<List<ReviewResponse>> Public(Guid building) =>
        (await factory.CreateClient().GetFromJsonAsync<List<ReviewResponse>>($"/api/v1/buildings/{building}/reviews", TestJson.Options))!;

    // Новый покупатель со сделкой: вход по коду и дом сделки.
    private async Task<(HttpClient Customer, Guid BuildingId)> CustomerWithDealAsync()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var building = await factory.WithDbAsync(db => db.Deals.IgnoreQueryFilters().Where(d => d.Id == deal.DealId)
            .Select(d => d.Apartment.Floor.BuildingId).SingleAsync());
        return (await factory.CreateCustomerClientAsync(deal.Phone), building);
    }
}

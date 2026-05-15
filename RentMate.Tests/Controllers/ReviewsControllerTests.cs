using System.Net;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;

namespace RentMate.Tests.Controllers;

public class ReviewsControllerTests : IntegrationTestBase
{
    private readonly string _ownerId;
    private readonly string _reviewerId;
    private readonly int _itemId;

    public ReviewsControllerTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner", onboardingCompleted: true);
        var reviewer = EntityFactory.CreateUser(firstName: "Reviewer", onboardingCompleted: true);
        var item = EntityFactory.CreateItem(userId: owner.Id, isListed: true);

        _ownerId = owner.Id;
        _reviewerId = reviewer.Id;
        _itemId = item.Id;

        SeedData(ctx =>
        {
            ctx.Users.AddRange(owner, reviewer);
            ctx.Items.Add(item);
        });
    }

    [Fact]
    public async Task Create_ValidReview_PersistsAndReturns201()
    {
        AuthenticateAs(_reviewerId);
        var response = await PostJsonAsync("/api/Reviews", new
        {
            itemId = _itemId,
            rating = 5,
            title = "Great",
            body = "Worked perfectly"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var db = GetDbContext();
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.ItemId == _itemId && r.ReviewerId == _reviewerId);
        Assert.NotNull(review);
        Assert.Equal(5, review!.Rating);
    }

    [Fact]
    public async Task Create_OwnItem_Forbidden()
    {
        AuthenticateAs(_ownerId);
        var response = await PostJsonAsync("/api/Reviews", new
        {
            itemId = _itemId,
            rating = 4,
            title = "Mine",
            body = "Reviewing my own item"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var db = GetDbContext();
        Assert.False(await db.Reviews.AnyAsync(r => r.ItemId == _itemId));
    }

    [Fact]
    public async Task Create_Duplicate_ReturnsBadRequest()
    {
        AuthenticateAs(_reviewerId);
        var body = new { itemId = _itemId, rating = 3, title = "First", body = "First review" };
        await PostJsonAsync("/api/Reviews", body);

        var second = await PostJsonAsync("/api/Reviews", body);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Create_Unauthenticated_IsRejected()
    {
        ClearAuthentication();
        var response = await PostJsonAsync("/api/Reviews", new { itemId = _itemId, rating = 5 });

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect,
            $"Expected 401/302, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetItemReviews_Anonymous_ReturnsList()
    {
        SeedData(ctx => ctx.Reviews.Add(
            EntityFactory.CreateReview(itemId: _itemId, reviewerId: _reviewerId)));

        ClearAuthentication();
        var response = await Client.GetAsync($"/api/Reviews/item/{_itemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"total\"", content);
    }
}

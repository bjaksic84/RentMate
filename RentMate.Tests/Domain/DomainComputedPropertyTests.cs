using RentMate.Models.Domain;
using RentMate.Services.Extensions;
using RentMate.Tests.Helpers;

namespace RentMate.Tests.Domain;

/// <summary>
/// Pure (no DbContext) tests for domain computed properties and the
/// rating/review extension helpers used by Profile and Item views.
/// </summary>
public class DomainComputedPropertyTests
{
    [Fact]
    public void PrimaryImageUrl_ReturnsLowestDisplayOrderImage()
    {
        var item = EntityFactory.CreateItem();
        item.Images = new List<ItemImage>
        {
            EntityFactory.CreateItemImage(itemId: item.Id, imageUrl: "second.jpg", displayOrder: 2),
            EntityFactory.CreateItemImage(itemId: item.Id, imageUrl: "primary.jpg", displayOrder: 0),
            EntityFactory.CreateItemImage(itemId: item.Id, imageUrl: "third.jpg", displayOrder: 5),
        };

        Assert.Equal("primary.jpg", item.PrimaryImageUrl);
    }

    [Fact]
    public void PrimaryImageUrl_NoImages_IsNull()
    {
        var item = EntityFactory.CreateItem();
        item.Images = new List<ItemImage>();

        Assert.Null(item.PrimaryImageUrl);
    }

    [Fact]
    public void CalculateRatingStats_ExcludesDeleted_AveragesActive()
    {
        var reviews = new List<Review>
        {
            EntityFactory.CreateReview(rating: 5, isDeleted: false),
            EntityFactory.CreateReview(rating: 3, isDeleted: false),
            EntityFactory.CreateReview(rating: 1, isDeleted: true), // ignored
        };

        var (avg, count) = reviews.CalculateRatingStats();

        Assert.Equal(2, count);
        Assert.Equal(4d, avg);
    }

    [Fact]
    public void CalculateRatingStats_EmptyOrNull_ReturnsZeroes()
    {
        Assert.Equal((0d, 0), new List<Review>().CalculateRatingStats());
        Assert.Equal((0d, 0), ((IEnumerable<Review>?)null).CalculateRatingStats());
    }

    [Fact]
    public void GetAllActiveReviews_FlattensItems_SkipsDeleted()
    {
        var user = EntityFactory.CreateUser();
        var item1 = EntityFactory.CreateItem(userId: user.Id);
        var item2 = EntityFactory.CreateItem(userId: user.Id);
        item1.Reviews = new List<Review>
        {
            EntityFactory.CreateReview(itemId: item1.Id, isDeleted: false),
            EntityFactory.CreateReview(itemId: item1.Id, isDeleted: true),
        };
        item2.Reviews = new List<Review>
        {
            EntityFactory.CreateReview(itemId: item2.Id, isDeleted: false),
        };
        user.Items = new List<Item> { item1, item2 };

        var active = user.GetAllActiveReviews().ToList();

        Assert.Equal(2, active.Count);
        Assert.All(active, r => Assert.False(r.IsDeleted));
    }

    [Fact]
    public void GetAllActiveReviews_NoItems_ReturnsEmpty()
    {
        var user = EntityFactory.CreateUser();
        user.Items = null;

        Assert.Empty(user.GetAllActiveReviews());
    }
}

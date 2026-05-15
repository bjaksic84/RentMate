using System.Net;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Controllers;

public class RentalsControllerTests : IntegrationTestBase
{
    private readonly string _ownerId;
    private readonly string _renterId;
    private readonly int _itemId;
    private readonly int _rentalId;

    public RentalsControllerTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner", onboardingCompleted: true, isGovernmentIdVerified: true);
        var renter = EntityFactory.CreateUser(firstName: "Renter", onboardingCompleted: true);
        var item = EntityFactory.CreateItem(userId: owner.Id, price: 20m, isListed: true);
        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Accepted, totalPrice: 200m);

        _ownerId = owner.Id;
        _renterId = renter.Id;
        _itemId = item.Id;
        _rentalId = rental.Id;

        SeedData(ctx =>
        {
            ctx.Users.AddRange(owner, renter);
            ctx.Items.Add(item);
            ctx.Rentals.Add(rental);
        });
    }

    [Fact]
    public async Task MarketplaceIndex_Anonymous_Returns200()
    {
        ClearAuthentication();
        var response = await Client.GetAsync("/Rentals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MarketplaceIndex_WithFilters_Returns200()
    {
        ClearAuthentication();
        var response = await Client.GetAsync("/Rentals?category=Electronics&minPrice=5&maxPrice=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestRental_ValidDates_CreatesPendingRental()
    {
        AuthenticateAs(_renterId);
        var start = DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-dd");
        var end = DateTime.UtcNow.AddDays(25).ToString("yyyy-MM-dd");

        var response = await PostFormAsync("/Rentals/RequestRental", new()
        {
            ["itemId"] = _itemId.ToString(),
            ["startDate"] = start,
            ["endDate"] = end
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var db = GetDbContext();
        var created = await db.Rentals.FirstOrDefaultAsync(r =>
            r.ItemId == _itemId && r.RenterId == _renterId && r.Status == RentalStatus.Pending);
        Assert.NotNull(created);
    }

    [Fact]
    public async Task ApproveRental_OwnerApproves_MarksAcceptedAndItemRented()
    {
        AuthenticateAs(_ownerId);
        var response = await PostFormAsync("/Rentals/ApproveRental", new()
        {
            ["rentalId"] = _rentalId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var db = GetDbContext();
        var rental = await db.Rentals.FindAsync(_rentalId);
        var item = await db.Items.FindAsync(_itemId);
        Assert.Equal(RentalStatus.Accepted, rental!.Status);
        Assert.True(item!.IsRented);
    }

    [Fact]
    public async Task CompleteRental_AfterApprove_MarksCompleted()
    {
        AuthenticateAs(_ownerId);
        await PostFormAsync("/Rentals/ApproveRental", new() { ["rentalId"] = _rentalId.ToString() });

        var response = await PostFormAsync("/Rentals/CompleteRental", new()
        {
            ["rentalId"] = _rentalId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var db = GetDbContext();
        var rental = await db.Rentals.FindAsync(_rentalId);
        var item = await db.Items.FindAsync(_itemId);
        Assert.Equal(RentalStatus.Completed, rental!.Status);
        Assert.False(item!.IsRented);
    }

    [Fact]
    public async Task CancelRental_OwnerCancels_MarksCancelled()
    {
        AuthenticateAs(_ownerId);
        var response = await PostFormAsync("/Rentals/CancelRental", new()
        {
            ["rentalId"] = _rentalId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var db = GetDbContext();
        var rental = await db.Rentals.FindAsync(_rentalId);
        Assert.Equal(RentalStatus.Cancelled, rental!.Status);
    }

    [Fact]
    public async Task RequestRental_Unauthenticated_Returns401()
    {
        ClearAuthentication();
        var response = await PostFormAsync("/Rentals/RequestRental", new()
        {
            ["itemId"] = _itemId.ToString(),
            ["startDate"] = "2026-06-01",
            ["endDate"] = "2026-06-05"
        });

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected 401/302, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetBookedDates_ReturnsJson()
    {
        AuthenticateAs(_renterId);
        var response = await Client.GetAsync($"/Items/GetBookedDates?itemId={_itemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", content.TrimStart());
    }
}

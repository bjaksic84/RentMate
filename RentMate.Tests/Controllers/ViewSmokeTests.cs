using System.Net;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Controllers;

/// <summary>
/// GET every major page → assert not 500.
/// Catches: view rendering crashes (null refs, missing model properties) after refactors.
/// Uses AllowAutoRedirect=true so auth redirects don't cause false failures.
/// </summary>
public class ViewSmokeTests : IClassFixture<RentMateWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly string _ownerId;
    private readonly string _renterId;
    private readonly string _adminId;
    private readonly int _itemId;
    private readonly int _rentalId;
    private readonly string _step2UserId;
    private readonly string _step3UserId;
    private readonly string _step4UserId;

    public ViewSmokeTests(RentMateWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true
        });

        var owner = EntityFactory.CreateUser(firstName: "Owner", isGovernmentIdVerified: true, onboardingCompleted: true);
        var renter = EntityFactory.CreateUser(firstName: "Renter", onboardingCompleted: true);
        var admin = EntityFactory.CreateUser(firstName: "Admin", onboardingCompleted: true);
        var item = EntityFactory.CreateItem(userId: owner.Id, isListed: true, depositAmount: 50m);
        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active, totalPrice: 100m);
        var deposit = EntityFactory.CreateDeposit(rentalId: rental.Id, amount: 50m, status: DepositStatus.Escalated);
        deposit.EscalatedAt = DateTime.UtcNow;

        // Onboarding-step users, each seeded to satisfy that step's guard.
        var step2User = EntityFactory.CreateUser(
            firstName: null, onboardingCompleted: false, userIntent: UserIntent.Both);
        var step3User = EntityFactory.CreateUser(
            firstName: "StepThree", lastName: "User", city: "Ljubljana",
            onboardingCompleted: false, userIntent: UserIntent.Both);
        var step4User = EntityFactory.CreateUser(
            firstName: "StepFour", lastName: "User", city: "Maribor",
            onboardingCompleted: false, userIntent: UserIntent.Renter);

        _ownerId = owner.Id;
        _renterId = renter.Id;
        _adminId = admin.Id;
        _itemId = item.Id;
        _rentalId = rental.Id;
        _step2UserId = step2User.Id;
        _step3UserId = step3User.Id;
        _step4UserId = step4User.Id;

        factory.SeedDatabase(ctx =>
        {
            ctx.Users.AddRange(owner, renter, admin, step2User, step3User, step4User);
            ctx.Items.Add(item);
            ctx.Rentals.Add(rental);
            ctx.RentalDeposits.Add(deposit);
        });
    }

    private void SetAuth(string? userId = null, string role = "User")
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.UserIdHeader);
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.RoleHeader);
        if (userId != null)
        {
            _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        }
    }

    /// <summary>Asserts response is not a server error (allows 200, 302, 401, 404 — just not 5xx).</summary>
    private static void AssertNotServerError(HttpResponseMessage response, string url)
    {
        Assert.True((int)response.StatusCode < 500,
            $"GET {url} returned {(int)response.StatusCode} {response.StatusCode}");
    }

    // ── Anonymous pages ────────────────────────────────────────────

    [Theory]
    [InlineData("/")]
    [InlineData("/Home/Privacy")]
    [InlineData("/Rentals")]
    public async Task AnonymousPage_RendersWithoutServerError(string url)
    {
        SetAuth();
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ItemDetails_RendersWithoutServerError()
    {
        SetAuth();
        var response = await _client.GetAsync($"/Items/Details/{_itemId}");
        AssertNotServerError(response, $"/Items/Details/{_itemId}");
    }

    [Fact]
    public async Task ProfileDetails_RendersWithoutServerError()
    {
        SetAuth();
        var response = await _client.GetAsync($"/Profile/Details/{_ownerId}");
        AssertNotServerError(response, $"/Profile/Details/{_ownerId}");
    }

    // ── Authenticated user pages ──────────────────────────────────

    [Fact]
    public async Task UserDashboard_RendersWithoutServerError()
    {
        SetAuth(_renterId);
        var response = await _client.GetAsync("/Dashboard");
        AssertNotServerError(response, "/Dashboard");
    }

    [Fact]
    public async Task ItemCreate_RendersWithoutServerError()
    {
        SetAuth(_ownerId);
        var response = await _client.GetAsync("/Items/Create");
        AssertNotServerError(response, "/Items/Create");
    }

    [Fact]
    public async Task ItemEdit_RendersWithoutServerError()
    {
        SetAuth(_ownerId);
        var response = await _client.GetAsync($"/Items/Edit/{_itemId}");
        AssertNotServerError(response, $"/Items/Edit/{_itemId}");
    }

    [Fact]
    public async Task OnboardingStep1_RendersWithoutServerError()
    {
        SetAuth(_renterId);
        var response = await _client.GetAsync("/Onboarding/Step1");
        AssertNotServerError(response, "/Onboarding/Step1");
    }

    [Theory]
    [InlineData(2, "/Onboarding/Step2")]
    [InlineData(3, "/Onboarding/Step3")]
    [InlineData(4, "/Onboarding/Step4")]
    public async Task OnboardingStep_RendersWithoutServerError(int step, string url)
    {
        var userId = step switch
        {
            2 => _step2UserId,
            3 => _step3UserId,
            _ => _step4UserId
        };
        SetAuth(userId);
        var response = await _client.GetAsync(url);
        AssertNotServerError(response, url);
    }

    // ── Admin pages ───────────────────────────────────────────────

    [Fact]
    public async Task AdminDashboard_RendersWithoutServerError()
    {
        SetAuth(_adminId, "Admin");
        var response = await _client.GetAsync("/Dashboard");
        AssertNotServerError(response, "/Dashboard (admin)");
    }

    [Fact]
    public async Task AdminUsers_RendersWithoutServerError()
    {
        SetAuth(_adminId, "Admin");
        var response = await _client.GetAsync("/Users");
        AssertNotServerError(response, "/Users");
    }

    [Fact]
    public async Task AdminResolvedDisputes_RendersWithoutServerError()
    {
        SetAuth(_adminId, "Admin");
        var response = await _client.GetAsync("/Dispute/AdminResolvedDisputes");
        AssertNotServerError(response, "/Dispute/AdminResolvedDisputes");
    }

    [Fact]
    public async Task AdminReviewDispute_RendersWithoutServerError()
    {
        SetAuth(_adminId, "Admin");
        var response = await _client.GetAsync($"/Dispute/AdminReviewDispute/{_rentalId}");
        AssertNotServerError(response, $"/Dispute/AdminReviewDispute/{_rentalId}");
    }

    [Fact]
    public async Task DisputeHistory_RendersWithoutServerError()
    {
        SetAuth(_ownerId);
        var response = await _client.GetAsync($"/Dispute/DisputeHistory/{_rentalId}");
        AssertNotServerError(response, $"/Dispute/DisputeHistory/{_rentalId}");
    }
}

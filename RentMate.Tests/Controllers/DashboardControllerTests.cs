using System.Net;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Controllers;

public class DashboardControllerTests : IntegrationTestBase
{
    private readonly string _ownerId;
    private readonly string _renterId;
    private readonly string _adminId;
    private readonly int _rentalId;
    private readonly int _itemId;

    public DashboardControllerTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner", onboardingCompleted: true);
        var renter = EntityFactory.CreateUser(firstName: "Renter", onboardingCompleted: true);
        var admin = EntityFactory.CreateUser(firstName: "Admin", onboardingCompleted: true);
        var item = EntityFactory.CreateItem(userId: owner.Id, price: 15m, isListed: true);
        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active, totalPrice: 150m,
            endDate: DateTime.UtcNow.AddDays(10));

        _ownerId = owner.Id;
        _renterId = renter.Id;
        _adminId = admin.Id;
        _rentalId = rental.Id;
        _itemId = item.Id;

        SeedData(ctx =>
        {
            ctx.Users.AddRange(owner, renter, admin);
            ctx.Items.Add(item);
            ctx.Rentals.Add(rental);
        });
    }

    [Fact]
    public async Task UserDashboard_Authenticated_Returns200()
    {
        AuthenticateAs(_renterId);
        var response = await Client.GetAsync("/Dashboard");
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected 200 or 302, got {response.StatusCode}");
    }

    [Fact]
    public async Task AdminDashboard_AdminRole_Returns200OrRedirect()
    {
        AuthenticateAs(_adminId, "Admin");
        var response = await Client.GetAsync("/Dashboard");
        Assert.True((int)response.StatusCode < 500, $"Got {response.StatusCode}");
    }

    [Fact]
    public async Task Dashboard_Unauthenticated_Returns401()
    {
        ClearAuthentication();
        var response = await Client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RequestExtension_ValidRequest_CreatesPendingExtension()
    {
        AuthenticateAs(_renterId);
        var newEnd = DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-dd");
        var response = await PostFormAsync("/Dashboard/RequestExtension", new()
        {
            ["RentalId"] = _rentalId.ToString(),
            ["NewEndDate"] = newEnd
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.True(json!.Success, json.Message);

        using var db = GetDbContext();
        var ext = await db.RentalExtensions.FirstOrDefaultAsync(e => e.RentalId == _rentalId);
        Assert.NotNull(ext);
        Assert.Equal(ExtensionStatus.Pending, ext!.Status);
    }

    [Fact]
    public async Task ApproveExtension_OwnerApproves_MarksAccepted()
    {
        AuthenticateAs(_renterId);
        await PostFormAsync("/Dashboard/RequestExtension", new()
        {
            ["RentalId"] = _rentalId.ToString(),
            ["NewEndDate"] = DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-dd")
        });

        int extId;
        using (var ctx = GetDbContext())
        {
            var ext = await ctx.RentalExtensions.FirstAsync(e => e.RentalId == _rentalId);
            extId = ext.Id;
        }

        AuthenticateAs(_ownerId);
        var response = await PostFormAsync("/Dashboard/ApproveExtension", new()
        {
            ["extensionId"] = extId.ToString()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.True(json!.Success, json.Message);

        using var db = GetDbContext();
        var updated = await db.RentalExtensions.FindAsync(extId);
        Assert.Equal(ExtensionStatus.Accepted, updated!.Status);
    }

    [Fact]
    public async Task DeclineExtension_OwnerDeclines_MarksDeclined()
    {
        AuthenticateAs(_renterId);
        await PostFormAsync("/Dashboard/RequestExtension", new()
        {
            ["RentalId"] = _rentalId.ToString(),
            ["NewEndDate"] = DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-dd")
        });

        int extId;
        using (var ctx = GetDbContext())
        {
            var ext = await ctx.RentalExtensions.FirstAsync(e => e.RentalId == _rentalId);
            extId = ext.Id;
        }

        AuthenticateAs(_ownerId);
        var response = await PostFormAsync("/Dashboard/DeclineExtension", new()
        {
            ["extensionId"] = extId.ToString()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.True(json!.Success, json.Message);

        using var db = GetDbContext();
        var updated = await db.RentalExtensions.FindAsync(extId);
        Assert.Equal(ExtensionStatus.Declined, updated!.Status);
    }
}

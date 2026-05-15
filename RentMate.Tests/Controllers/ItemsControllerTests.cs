using System.Net;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Controllers;

public class ItemsControllerTests : IntegrationTestBase
{
    private readonly string _ownerId;
    private readonly string _renterId;
    private readonly string _adminId;
    private readonly int _itemId;

    public ItemsControllerTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner", onboardingCompleted: true, isGovernmentIdVerified: true);
        var renter = EntityFactory.CreateUser(firstName: "Renter", onboardingCompleted: true);
        var admin = EntityFactory.CreateUser(firstName: "Admin", onboardingCompleted: true);
        var item = EntityFactory.CreateItem(userId: owner.Id, price: 25m, isListed: true);

        _ownerId = owner.Id;
        _renterId = renter.Id;
        _adminId = admin.Id;
        _itemId = item.Id;

        SeedData(ctx =>
        {
            ctx.Users.AddRange(owner, renter, admin);
            ctx.Items.Add(item);
        });
    }

    [Fact]
    public async Task Create_ValidItem_PersistsItemAndRedirects()
    {
        AuthenticateAs(_ownerId);
        var response = await PostFormAsync("/Items/Create", new()
        {
            ["Title"] = "Test Camera",
            ["Description"] = "A great camera for rent",
            ["Price"] = "30",
            ["Category"] = "Electronics",
            ["Location"] = "Ljubljana"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Dashboard", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        using var db = GetDbContext();
        var created = await db.Items.FirstOrDefaultAsync(i => i.Title == "Test Camera");
        Assert.NotNull(created);
        Assert.Equal(30m, created!.Price);
        Assert.Equal(_ownerId, created.UserId);
        Assert.False(created.IsListed); // new items start unlisted
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        ClearAuthentication();
        var response = await PostFormAsync("/Items/Create", new()
        {
            ["Title"] = "Test",
            ["Price"] = "10",
            ["Category"] = "Tools"
        });

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected 401/302, got {response.StatusCode}");

        // Nothing should have been persisted.
        using var db = GetDbContext();
        Assert.False(await db.Items.AnyAsync(i => i.Title == "Test"));
    }

    [Fact]
    public async Task Edit_OwnerCanEdit_PersistsChanges()
    {
        AuthenticateAs(_ownerId);
        var response = await PostFormAsync($"/Items/Edit/{_itemId}", new()
        {
            ["Id"] = _itemId.ToString(),
            ["Title"] = "Updated Camera",
            ["Description"] = "Updated description",
            ["Price"] = "35",
            ["Category"] = "Electronics",
            ["IsListed"] = "true"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var db = GetDbContext();
        var item = await db.Items.FindAsync(_itemId);
        Assert.NotNull(item);
        Assert.Equal("Updated Camera", item!.Title);
        Assert.Equal("Updated description", item.Description);
        Assert.Equal(35m, item.Price);
    }

    [Fact]
    public async Task ToggleListing_FlipsListedFlagInDb()
    {
        AuthenticateAs(_ownerId);
        // Seeded item starts IsListed = true → toggling turns it off.
        var response = await PostFormAsync($"/Items/ToggleListing/{_itemId}", new());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", content.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);

        using var db = GetDbContext();
        var item = await db.Items.FindAsync(_itemId);
        Assert.NotNull(item);
        Assert.False(item!.IsListed);
    }

    [Fact]
    public async Task AdminToggleHide_AdminOnly_SetsHiddenFlag()
    {
        AuthenticateAs(_adminId, "Admin");
        var response = await PostFormAsync($"/Items/AdminToggleHide/{_itemId}", new());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = GetDbContext();
        var item = await db.Items.FindAsync(_itemId);
        Assert.NotNull(item);
        Assert.True(item!.IsAdminHidden);
    }

    [Fact]
    public async Task AdminToggleHide_NonAdmin_Forbidden()
    {
        AuthenticateAs(_renterId);
        var response = await PostFormAsync($"/Items/AdminToggleHide/{_itemId}", new());
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 403/401, got {response.StatusCode}");

        // The flag must not have changed.
        using var db = GetDbContext();
        var item = await db.Items.FindAsync(_itemId);
        Assert.False(item!.IsAdminHidden);
    }

    [Fact]
    public async Task GetAccessories_OwnerGetsJsonArray()
    {
        AuthenticateAs(_ownerId);
        var response = await Client.GetAsync($"/Items/GetAccessories?itemId={_itemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", content.TrimStart());
    }
}

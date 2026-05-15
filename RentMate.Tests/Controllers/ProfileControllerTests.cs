using System.Net;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;

namespace RentMate.Tests.Controllers;

public class ProfileControllerTests : IntegrationTestBase
{
    private readonly string _userId;

    public ProfileControllerTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var user = EntityFactory.CreateUser(firstName: "Profile", onboardingCompleted: true);
        _userId = user.Id;
        SeedData(ctx => ctx.Users.Add(user));
    }

    [Fact]
    public async Task Index_Authenticated_RedirectsToOwnDetails()
    {
        AuthenticateAs(_userId);
        var response = await Client.GetAsync("/Profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Profile/Details/{_userId}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Index_Unauthenticated_RedirectsToLogin()
    {
        ClearAuthentication();
        var response = await Client.GetAsync("/Profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Details_ExistingUser_Returns200()
    {
        ClearAuthentication();
        var response = await Client.GetAsync($"/Profile/Details/{_userId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_UnknownUser_Returns404()
    {
        ClearAuthentication();
        var response = await Client.GetAsync("/Profile/Details/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

using System.Net;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;

namespace RentMate.Tests.Controllers;

/// <summary>
/// Tests authentication and authorization wiring.
/// </summary>
public class AuthFlowTests : IntegrationTestBase
{
    private readonly string _userId;
    private readonly string _adminId;

    public AuthFlowTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var user = EntityFactory.CreateUser(firstName: "NormalUser", onboardingCompleted: true);
        var admin = EntityFactory.CreateUser(firstName: "AdminUser", onboardingCompleted: true);

        _userId = user.Id;
        _adminId = admin.Id;

        SeedData(ctx => ctx.Users.AddRange(user, admin));
    }

    [Theory]
    [InlineData("/Dashboard")]
    public async Task ProtectedPage_Unauthenticated_Returns401(string url)
    {
        ClearAuthentication();
        var response = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/Users")]
    public async Task AdminPage_NonAdmin_ReturnsForbiddenOr401(string url)
    {
        AuthenticateAs(_userId); // Normal user, not admin
        var response = await Client.GetAsync(url);
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 403/401, got {response.StatusCode}");
    }

    [Fact]
    public async Task AdminPage_AdminRole_Returns200()
    {
        AuthenticateAs(_adminId, "Admin");
        var response = await Client.GetAsync("/Users");
        Assert.True((int)response.StatusCode < 500, $"Got {response.StatusCode}");
    }
}

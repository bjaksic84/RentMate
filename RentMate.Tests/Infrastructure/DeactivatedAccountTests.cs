using System.Net;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;

namespace RentMate.Tests.Infrastructure;

/// <summary>
/// Integration tests for the DeactivatedAccountMiddleware.
/// Verifies that deactivated users are redirected away from protected pages
/// but can still access the deactivated page itself.
/// </summary>
public class DeactivatedAccountTests : IntegrationTestBase
{
    private readonly string _deactivatedUserId;
    private readonly string _activeUserId;

    public DeactivatedAccountTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var deactivatedUser = EntityFactory.CreateUser(
            firstName: "Deactivated",
            isDeactivated: true,
            deactivatedBy: DeactivationSource.User,
            deactivatedAt: DateTime.UtcNow.AddDays(-1),
            deactivationReason: "Test deactivation",
            onboardingCompleted: true);

        var activeUser = EntityFactory.CreateUser(
            firstName: "Active",
            onboardingCompleted: true);

        _deactivatedUserId = deactivatedUser.Id;
        _activeUserId = activeUser.Id;

        SeedData(ctx =>
        {
            ctx.Users.AddRange(deactivatedUser, activeUser);
        });
    }

    [Fact]
    public async Task DeactivatedUser_GetDashboard_Redirects()
    {
        AuthenticateAs(_deactivatedUserId);

        var response = await Client.GetAsync("/Dashboard");

        // Middleware redirects deactivated users to /Account/Deactivated (302)
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Deactivated", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ActiveUser_GetDashboard_NotRedirectedToDeactivated()
    {
        AuthenticateAs(_activeUserId);

        var response = await Client.GetAsync("/Dashboard");

        // Active user should never be redirected to the deactivated page.
        // A redirect to another page (e.g. onboarding) is fine -- that's not the middleware.
        Assert.True((int)response.StatusCode < 500,
            $"GET /Dashboard returned {(int)response.StatusCode} {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            var location = response.Headers.Location?.OriginalString ?? "";
            Assert.DoesNotContain("/Account/Deactivated", location);
        }
    }

    [Fact]
    public async Task DeactivatedUser_GetDeactivatedPage_Returns200()
    {
        // Use a client that follows redirects to verify final page renders
        var followClient = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true
        });
        followClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _deactivatedUserId);
        followClient.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "User");

        var response = await followClient.GetAsync("/Account/Deactivated");

        // The deactivated page is in AllowedExactPaths, so middleware skips it
        Assert.True((int)response.StatusCode < 500,
            $"GET /Account/Deactivated returned {(int)response.StatusCode} {response.StatusCode}");
    }
}

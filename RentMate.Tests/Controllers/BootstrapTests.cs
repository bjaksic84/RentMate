using System.Net;
using RentMate.Tests.Infrastructure;

namespace RentMate.Tests.Controllers;

/// <summary>
/// Verifies the WebApplicationFactory infrastructure works — app boots and serves pages.
/// </summary>
public class BootstrapTests : IntegrationTestBase
{
    public BootstrapTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        // Ensure DB schema exists
        factory.SeedDatabase(_ => { });
    }

    [Fact]
    public async Task Homepage_Returns200()
    {
        var response = await Client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedPage_Unauthenticated_Returns401()
    {
        ClearAuthentication();
        var response = await Client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

using System.Net;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;

namespace RentMate.Tests.Controllers;

/// <summary>
/// PaymentController is [Authorize] and Stripe-backed (IPaymentService is
/// mocked by the factory). These cover the deterministic guard paths only —
/// auth and not-found — not the Stripe-dependent happy path.
/// </summary>
public class PaymentControllerTests : IntegrationTestBase
{
    private readonly string _userId;

    public PaymentControllerTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var user = EntityFactory.CreateUser(firstName: "Payer", onboardingCompleted: true);
        _userId = user.Id;
        SeedData(ctx => ctx.Users.Add(user));
    }

    [Fact]
    public async Task Checkout_Unauthenticated_IsRejected()
    {
        ClearAuthentication();
        var response = await Client.GetAsync("/Payment/Checkout?rentalId=1");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect,
            $"Expected 401/302, got {response.StatusCode}");
    }

    [Fact]
    public async Task Checkout_UnknownRental_Returns404()
    {
        AuthenticateAs(_userId);
        var response = await Client.GetAsync("/Payment/Checkout?rentalId=999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

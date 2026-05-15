using System.Net;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;

namespace RentMate.Tests.Controllers;

/// <summary>
/// Integration tests for the 4-step onboarding wizard.
/// Verifies step guards, redirect logic, and completion flow.
/// </summary>
public class OnboardingControllerTests : IntegrationTestBase
{
    public OnboardingControllerTests(RentMateWebApplicationFactory factory) : base(factory) { }

    /// <summary>Asserts the response redirects to the home page (root "/").</summary>
    private static void AssertRedirectsHome(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.True(location == "/" || location.Contains("/Home", StringComparison.OrdinalIgnoreCase),
            $"Expected redirect to home, got: {location}");
    }

    #region Step 1: Intent Selection

    [Fact]
    public async Task Step1_Get_ReturnsOk()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: null,
            lastName: null,
            city: null,
            userIntent: null);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Step1_Get_AlreadyCompleted_RedirectsHome()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: true,
            userIntent: UserIntent.Both,
            firstName: "Done");

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step1");

        AssertRedirectsHome(response);
    }

    [Fact]
    public async Task Step1_Get_Unauthenticated_ReturnsUnauthorized()
    {
        ClearAuthentication();

        var response = await Client.GetAsync("/Onboarding/Step1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Step 2: Name + Location

    [Fact]
    public async Task Step2_Get_NoIntent_RedirectsToStep1()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: null,
            lastName: null,
            city: null,
            userIntent: null);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step2");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Step1", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Step2_Get_WithIntent_ReturnsOk()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: null,
            lastName: null,
            city: null,
            userIntent: UserIntent.Renter);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Step2_Get_AlreadyCompleted_RedirectsHome()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: true,
            userIntent: UserIntent.Lister,
            firstName: "Done");

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step2");

        AssertRedirectsHome(response);
    }

    #endregion

    #region Step 3: Photo + Bio

    [Fact]
    public async Task Step3_Get_NoName_RedirectsToStep2()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: null,
            lastName: null,
            city: null,
            userIntent: UserIntent.Both);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step3");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Step2", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Step3_Get_NoIntent_RedirectsToStep1()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: "Hack",
            lastName: "Attempt",
            city: null,
            userIntent: null);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step3");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Step1", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Step3_Get_NoLastName_RedirectsToStep2()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: "Alice",
            lastName: null,
            city: null,
            userIntent: UserIntent.Renter);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step3");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Step2", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Step3_Get_WithName_ReturnsOk()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: "Alice",
            lastName: "Test",
            city: null,
            userIntent: UserIntent.Renter);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Step3_Get_AlreadyCompleted_RedirectsHome()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: true,
            userIntent: UserIntent.Both,
            firstName: "Done");

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step3");

        AssertRedirectsHome(response);
    }

    #endregion

    #region Step 4: Carousel Tour

    [Fact]
    public async Task Step4_Get_NoName_RedirectsToStep2()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: null,
            lastName: null,
            city: null,
            userIntent: UserIntent.Lister);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step4");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Step2", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Step4_Get_NoIntent_RedirectsToStep1()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: "Hack",
            lastName: "Attempt",
            city: null,
            userIntent: null);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step4");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Step1", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Step4_Get_WithName_ReturnsOk()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: "Bob",
            lastName: "Test",
            city: "Ljubljana",
            userIntent: UserIntent.Both);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step4");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Step4_Get_AlreadyCompleted_RedirectsHome()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: true,
            userIntent: UserIntent.Both,
            firstName: "Done");

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await Client.GetAsync("/Onboarding/Step4");

        AssertRedirectsHome(response);
    }

    #endregion

    #region Complete Onboarding

    [Fact]
    public async Task CompleteOnboarding_Post_RedirectsHome()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: "Alice",
            lastName: "Test",
            city: "Ljubljana",
            userIntent: UserIntent.Both);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/CompleteOnboarding", new Dictionary<string, string>());

        AssertRedirectsHome(response);
    }

    [Fact]
    public async Task CompleteOnboarding_Post_Unauthenticated_ReturnsUnauthorized()
    {
        ClearAuthentication();

        var response = await PostFormAsync("/Onboarding/CompleteOnboarding", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_Post_SetsOnboardingCompletedTrueInDb()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: "Alice",
            lastName: "Test",
            city: "Ljubljana",
            userIntent: UserIntent.Both);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/CompleteOnboarding", new Dictionary<string, string>());

        AssertRedirectsHome(response);

        using var ctx = GetDbContext();
        var saved = ctx.Users.Single(u => u.Id == user.Id);
        Assert.True(saved.OnboardingCompleted);
    }

    [Fact]
    public async Task CompleteOnboarding_Post_Replay_IsIdempotent()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: true,
            userIntent: UserIntent.Both,
            firstName: "Done",
            lastName: "User",
            city: "Ljubljana");

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/CompleteOnboarding", new Dictionary<string, string>());

        AssertRedirectsHome(response);

        using var ctx = GetDbContext();
        var saved = ctx.Users.Single(u => u.Id == user.Id);
        Assert.True(saved.OnboardingCompleted);
    }

    #endregion

    #region Spotlight

    [Fact]
    public async Task MarkSpotlightComplete_Post_SetsFlagAndReturnsNoContent()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: true,
            userIntent: UserIntent.Both,
            firstName: "Alice",
            lastName: "Test");

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/MarkSpotlightComplete", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var ctx = GetDbContext();
        var saved = ctx.Users.Single(u => u.Id == user.Id);
        Assert.True(saved.SpotlightTourCompleted);
    }

    [Fact]
    public async Task MarkSpotlightComplete_Post_Replay_IsIdempotent()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: true,
            userIntent: UserIntent.Both,
            firstName: "Alice",
            lastName: "Test");
        user.SpotlightTourCompleted = true;

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/MarkSpotlightComplete", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MarkSpotlightComplete_Post_Unauthenticated_ReturnsUnauthorized()
    {
        ClearAuthentication();

        var response = await PostFormAsync("/Onboarding/MarkSpotlightComplete", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region POST behavior

    [Fact]
    public async Task Step1_Post_SavesUserIntent()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: null,
            lastName: null,
            city: null,
            userIntent: null);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/Step1", new Dictionary<string, string>
        {
            ["SelectedIntent"] = nameof(UserIntent.Renter)
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Step2", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);

        using var ctx = GetDbContext();
        var saved = ctx.Users.Single(u => u.Id == user.Id);
        Assert.Equal(UserIntent.Renter, saved.UserIntent);
    }

    [Fact]
    public async Task Step2_Post_SavesNameAndCity()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: null,
            lastName: null,
            city: null,
            userIntent: UserIntent.Both);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/Step2", new Dictionary<string, string>
        {
            ["FirstName"] = "Alice",
            ["LastName"] = "Smith",
            ["ShareLocation"] = "true",
            ["City"] = "Ljubljana (Center)"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Step3", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);

        using var ctx = GetDbContext();
        var saved = ctx.Users.Single(u => u.Id == user.Id);
        Assert.Equal("Alice", saved.FirstName);
        Assert.Equal("Smith", saved.LastName);
        Assert.Equal("Ljubljana (Center)", saved.City);
    }

    [Fact]
    public async Task Step2_Post_InvalidCity_ReturnsViewWithLocalizedError()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: null,
            lastName: null,
            city: null,
            userIntent: UserIntent.Both);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/Step2", new Dictionary<string, string>
        {
            ["FirstName"] = "Alice",
            ["LastName"] = "Smith",
            ["ShareLocation"] = "true",
            ["City"] = "BogusCityNotInList"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Step2_Post_DeclinedLocation_ClearsCity()
    {
        var user = EntityFactory.CreateUser(
            onboardingCompleted: false,
            firstName: "Old",
            lastName: "Name",
            city: "Maribor",
            userIntent: UserIntent.Both);

        SeedData(ctx => ctx.Users.Add(user));
        AuthenticateAs(user.Id);

        var response = await PostFormAsync("/Onboarding/Step2", new Dictionary<string, string>
        {
            ["FirstName"] = "Alice",
            ["LastName"] = "Smith",
            ["ShareLocation"] = "false"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var ctx = GetDbContext();
        var saved = ctx.Users.Single(u => u.Id == user.Id);
        Assert.Null(saved.City);
    }

    #endregion
}

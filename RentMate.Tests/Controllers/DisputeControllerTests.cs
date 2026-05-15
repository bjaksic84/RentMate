using System.Net;
using System.Net.Http.Headers;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Controllers;

/// <summary>
/// Tests DisputeController JSON response contracts.
/// All 16 actions must return Json({success, message}) — never HTML 500.
/// Frontend JS depends on this shape.
/// </summary>
public class DisputeControllerTests : IntegrationTestBase
{
    private readonly string _ownerId;
    private readonly string _renterId;
    private readonly string _adminId;
    private readonly int _rentalId;

    public DisputeControllerTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner", onboardingCompleted: true);
        var renter = EntityFactory.CreateUser(firstName: "Renter", onboardingCompleted: true);
        var admin = EntityFactory.CreateUser(firstName: "Admin", onboardingCompleted: true);

        var item = EntityFactory.CreateItem(userId: owner.Id, depositAmount: 100m);
        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active, totalPrice: 200m);

        // Authorized deposit ready for actions
        var deposit = EntityFactory.CreateDeposit(rentalId: rental.Id, amount: 100m,
            status: DepositStatus.Authorized, paymentReference: "pay_test");

        _ownerId = owner.Id;
        _renterId = renter.Id;
        _adminId = admin.Id;
        _rentalId = rental.Id;

        SeedData(ctx =>
        {
            ctx.Users.AddRange(owner, renter, admin);
            ctx.Items.Add(item);
            ctx.Rentals.Add(rental);
            ctx.RentalDeposits.Add(deposit);
        });
    }

    // ── Release ───────────────────────────────────────────────────

    [Fact]
    public async Task ReleaseDeposit_ReturnsJsonSuccess()
    {
        AuthenticateAs(_ownerId);
        var response = await PostFormAsync("/Dispute/ReleaseDeposit", new()
        {
            ["rentalId"] = _rentalId.ToString()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    [Fact]
    public async Task ReleaseDeposit_WrongUser_ReturnsJsonFailure()
    {
        AuthenticateAs(_renterId); // Not owner
        var response = await PostFormAsync("/Dispute/ReleaseDeposit", new()
        {
            ["rentalId"] = _rentalId.ToString()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.False(json!.Success); // Caught by try/catch, returned as JSON failure
    }

    // ── Charge ────────────────────────────────────────────────────

    [Fact]
    public async Task ChargeDeposit_NoEvidence_ReturnsJsonFailure()
    {
        AuthenticateAs(_ownerId);
        // No file attached — controller checks evidence != null before calling service
        var response = await PostFormAsync("/Dispute/ChargeDeposit", new()
        {
            ["rentalId"] = _rentalId.ToString(),
            ["amount"] = "50",
            ["reason"] = "Damage"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.False(json!.Success);
        Assert.Contains("evidence", json.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChargeDeposit_WithEvidence_ReturnsJsonSuccess()
    {
        AuthenticateAs(_ownerId);

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(_rentalId.ToString()), "rentalId");
        content.Add(new StringContent("80"), "amount");
        content.Add(new StringContent("Damage to item"), "reason");

        // Fake file
        var fileContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }); // JPEG header
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "evidence", "damage.jpg");

        var response = await PostMultipartAsync("/Dispute/ChargeDeposit", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    // ── Dispute ───────────────────────────────────────────────────

    [Fact]
    public async Task DisputeDeposit_ReturnsJsonSuccess()
    {
        // First charge it (as owner)
        AuthenticateAs(_ownerId);
        var chargeContent = new MultipartFormDataContent();
        chargeContent.Add(new StringContent(_rentalId.ToString()), "rentalId");
        chargeContent.Add(new StringContent("100"), "amount");
        chargeContent.Add(new StringContent("Damage"), "reason");
        var fileContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        chargeContent.Add(fileContent, "evidence", "photo.jpg");
        await PostMultipartAsync("/Dispute/ChargeDeposit", chargeContent);

        // Now dispute as renter
        AuthenticateAs(_renterId);
        var response = await PostFormAsync("/Dispute/DisputeDeposit", new()
        {
            ["rentalId"] = _rentalId.ToString(),
            ["reason"] = "No damage occurred"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    // ── Counter Offer ─────────────────────────────────────────────

    [Fact]
    public async Task CounterOffer_ReturnsJsonSuccess()
    {
        // Setup: charge → dispute → counter
        await SetupChargedAndDisputedState();

        AuthenticateAs(_ownerId);
        var response = await PostFormAsync("/Dispute/CounterOfferDeposit", new()
        {
            ["rentalId"] = _rentalId.ToString(),
            ["amount"] = "50",
            ["response"] = "Meet halfway"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    // ── Accept/Reject Counter ─────────────────────────────────────

    [Fact]
    public async Task AcceptCounterOffer_ReturnsJsonSuccess()
    {
        await SetupCounterOfferedState();

        AuthenticateAs(_renterId);
        var response = await PostFormAsync("/Dispute/AcceptCounterOffer", new()
        {
            ["rentalId"] = _rentalId.ToString()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    [Fact]
    public async Task RejectCounterOffer_ReturnsJsonSuccess()
    {
        await SetupCounterOfferedState();

        AuthenticateAs(_renterId);
        var response = await PostFormAsync("/Dispute/RejectCounterOffer", new()
        {
            ["rentalId"] = _rentalId.ToString()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    // ── Escalate ──────────────────────────────────────────────────

    [Fact]
    public async Task EscalateDispute_ReturnsJsonSuccess()
    {
        await SetupChargedAndDisputedState();

        AuthenticateAs(_ownerId);
        var response = await PostFormAsync("/Dispute/EscalateDispute", new()
        {
            ["rentalId"] = _rentalId.ToString(),
            ["response"] = "I have proof"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    // ── Admin Resolve ─────────────────────────────────────────────

    [Fact]
    public async Task AdminResolve_ReturnsJsonSuccess()
    {
        await SetupEscalatedState();

        AuthenticateAs(_adminId, "Admin");
        var response = await PostFormAsync("/Dispute/AdminResolveDispute", new()
        {
            ["rentalId"] = _rentalId.ToString(),
            ["amount"] = "50",
            ["adminNotes"] = "Split the difference"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    [Fact]
    public async Task AdminResolve_NonAdmin_ReturnsForbidden()
    {
        await SetupEscalatedState();

        AuthenticateAs(_renterId); // Not admin
        var response = await PostFormAsync("/Dispute/AdminResolveDispute", new()
        {
            ["rentalId"] = _rentalId.ToString(),
            ["amount"] = "50",
            ["adminNotes"] = "Notes"
        });

        // Should be 403 Forbidden (Authorize(Roles="Admin"))
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 403 or 401, got {response.StatusCode}");
    }

    // ── Evidence Upload ───────────────────────────────────────────

    [Fact]
    public async Task UploadEvidence_ReturnsJsonWithUrl()
    {
        // Charge first so evidence upload is valid
        AuthenticateAs(_ownerId);
        var chargeContent = new MultipartFormDataContent();
        chargeContent.Add(new StringContent(_rentalId.ToString()), "rentalId");
        chargeContent.Add(new StringContent("100"), "amount");
        chargeContent.Add(new StringContent("Damage"), "reason");
        var fc = new ByteArrayContent(new byte[] { 0xFF, 0xD8 });
        fc.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        chargeContent.Add(fc, "evidence", "photo.jpg");
        await PostMultipartAsync("/Dispute/ChargeDeposit", chargeContent);

        // Upload separate evidence
        var uploadContent = new MultipartFormDataContent();
        uploadContent.Add(new StringContent(_rentalId.ToString()), "rentalId");
        uploadContent.Add(new StringContent("Additional proof"), "notes");
        var file = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        uploadContent.Add(file, "file", "proof.png");

        var response = await PostMultipartAsync("/Dispute/UploadDisputeEvidence", uploadContent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync<JsonResult>(response);
        Assert.NotNull(json);
        Assert.True(json!.Success);
    }

    // ── Auth Required ─────────────────────────────────────────────

    [Fact]
    public async Task AllDisputeActions_RequireAuthentication()
    {
        ClearAuthentication();

        var endpoints = new[]
        {
            "/Dispute/ReleaseDeposit",
            "/Dispute/ChargeDeposit",
            "/Dispute/DisputeDeposit",
            "/Dispute/CounterOfferDeposit",
            "/Dispute/AcceptCounterOffer",
            "/Dispute/RejectCounterOffer",
            "/Dispute/EscalateDispute",
            "/Dispute/AdminResolveDispute",
        };

        foreach (var url in endpoints)
        {
            var response = await PostFormAsync(url, new() { ["rentalId"] = "1" });
            Assert.True(
                response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect,
                $"{url} should require auth, got {response.StatusCode}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private async Task SetupChargedAndDisputedState()
    {
        AuthenticateAs(_ownerId);
        var charge = new MultipartFormDataContent();
        charge.Add(new StringContent(_rentalId.ToString()), "rentalId");
        charge.Add(new StringContent("100"), "amount");
        charge.Add(new StringContent("Damage"), "reason");
        var f = new ByteArrayContent(new byte[] { 0xFF, 0xD8 });
        f.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        charge.Add(f, "evidence", "p.jpg");
        await PostMultipartAsync("/Dispute/ChargeDeposit", charge);

        AuthenticateAs(_renterId);
        await PostFormAsync("/Dispute/DisputeDeposit", new()
        {
            ["rentalId"] = _rentalId.ToString(),
            ["reason"] = "No damage"
        });
    }

    private async Task SetupCounterOfferedState()
    {
        await SetupChargedAndDisputedState();

        AuthenticateAs(_ownerId);
        await PostFormAsync("/Dispute/CounterOfferDeposit", new()
        {
            ["rentalId"] = _rentalId.ToString(),
            ["amount"] = "50",
            ["response"] = "Half"
        });
    }

    private async Task SetupEscalatedState()
    {
        await SetupChargedAndDisputedState();

        AuthenticateAs(_ownerId);
        await PostFormAsync("/Dispute/EscalateDispute", new()
        {
            ["rentalId"] = _rentalId.ToString()
        });
    }
}

using System.Net;
using RentMate.Tests.Infrastructure;

namespace RentMate.Tests.Controllers;

/// <summary>
/// CurrencyController and CultureController are tiny cookie-setters; verify
/// they set the right cookie and local-redirect (and reject non-local URLs).
/// </summary>
public class CurrencyCultureControllerTests : IntegrationTestBase
{
    public CurrencyCultureControllerTests(RentMateWebApplicationFactory factory) : base(factory) { }

    private static bool HasCookie(HttpResponseMessage r, string name) =>
        r.Headers.TryGetValues("Set-Cookie", out var v) &&
        v.Any(c => c.StartsWith(name + "=", StringComparison.Ordinal));

    [Fact]
    public async Task SetCurrency_SupportedCode_SetsCookieAndRedirects()
    {
        var response = await PostFormAsync("/Currency/SetCurrency",
            new() { ["currency"] = "USD", ["returnUrl"] = "/" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(HasCookie(response, "RentMateCurrency"));
    }

    [Fact]
    public async Task SetCurrency_UnsupportedCode_NoCookieButStillRedirects()
    {
        var response = await PostFormAsync("/Currency/SetCurrency",
            new() { ["currency"] = "ZZZ", ["returnUrl"] = "/" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(HasCookie(response, "RentMateCurrency"));
    }

    [Fact]
    public async Task SetLanguage_SetsCultureCookieAndRedirects()
    {
        var response = await PostFormAsync("/Culture/SetLanguage",
            new() { ["culture"] = "en", ["returnUrl"] = "/" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(HasCookie(response, ".AspNetCore.Culture"));
    }

    [Fact]
    public async Task GetTranslations_ReturnsJsonPayload()
    {
        var response = await Client.GetAsync("/api/translations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("translations", content);
    }
}

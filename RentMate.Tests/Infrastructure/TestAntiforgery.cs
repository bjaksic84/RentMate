using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace RentMate.Tests.Infrastructure;

/// <summary>
/// No-op antiforgery implementation for integration tests.
/// All validation passes, no tokens required.
/// </summary>
public class TestAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
        => new("test-request-token", "test-cookie-token", "test-form-field", "test-header");

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext)
        => new("test-request-token", "test-cookie-token", "test-form-field", "test-header");

    public Task<bool> IsRequestValidAsync(HttpContext httpContext)
        => Task.FromResult(true);

    public Task ValidateRequestAsync(HttpContext httpContext)
        => Task.CompletedTask;

    public void SetCookieTokenAndHeader(HttpContext httpContext) { }
}

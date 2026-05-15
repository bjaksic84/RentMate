using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RentMate.Infrastructure.Data;

namespace RentMate.Tests.Infrastructure;

/// <summary>
/// Base class for controller integration tests.
/// Provides authenticated HttpClient, data seeding, and JSON helpers.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<RentMateWebApplicationFactory>, IDisposable
{
    protected readonly RentMateWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected IntegrationTestBase(RentMateWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Per-test isolation: the factory (IClassFixture) keeps one SQLite
        // :memory: connection for its whole lifetime, so without this every
        // test in a class would share accumulated rows. The xUnit test-class
        // constructor runs once per test and calls this base ctor first, so
        // resetting here gives each test a clean DB. Derived ctors then seed.
        Factory.ResetDatabase();
    }

    /// <summary>Sets auth headers so subsequent requests authenticate as the given user/role.</summary>
    protected void AuthenticateAs(string userId, string role = "User")
    {
        Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserIdHeader);
        Client.DefaultRequestHeaders.Remove(TestAuthHandler.RoleHeader);
        Client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        Client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
    }

    /// <summary>Clears auth headers for anonymous requests.</summary>
    protected void ClearAuthentication()
    {
        Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserIdHeader);
        Client.DefaultRequestHeaders.Remove(TestAuthHandler.RoleHeader);
    }

    /// <summary>Seeds the test database with entities.</summary>
    protected void SeedData(Action<RentMateContext> seed) => Factory.SeedDatabase(seed);

    /// <summary>Gets a fresh DbContext for post-request assertions.</summary>
    protected RentMateContext GetDbContext() => Factory.GetDbContext();

    /// <summary>POST form data (like a browser form submission).</summary>
    protected Task<HttpResponseMessage> PostFormAsync(string url, Dictionary<string, string> formData)
    {
        var content = new FormUrlEncodedContent(formData);
        return Client.PostAsync(url, content);
    }

    /// <summary>POST JSON body.</summary>
    protected Task<HttpResponseMessage> PostJsonAsync(string url, object data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return Client.PostAsync(url, content);
    }

    /// <summary>POST multipart form data (for file uploads).</summary>
    protected Task<HttpResponseMessage> PostMultipartAsync(string url, MultipartFormDataContent content)
    {
        return Client.PostAsync(url, content);
    }

    /// <summary>Deserializes JSON response into the given type.</summary>
    protected static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    /// <summary>Standard JSON response shape from DisputeController and other AJAX endpoints.</summary>
    protected record JsonResult(bool Success, string? Message);

    public virtual void Dispose()
    {
        Client.Dispose();
    }
}

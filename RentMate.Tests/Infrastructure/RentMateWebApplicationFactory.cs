using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Implementations;
using RentMate.Services.Interfaces;

namespace RentMate.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that replaces Postgres with SQLite in-memory,
/// removes background services, and adds test authentication.
/// Uses SQLite (not EF InMemory) because controllers use ExecuteUpdateAsync.
/// </summary>
public class RentMateWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public RentMateWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Provide dummy config values so Program.cs doesn't throw on missing secrets
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
                ["Jwt:Key"] = "TestKeyThatIsAtLeast32CharactersLongForHmac256",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Cloudinary:CloudName"] = "test",
                ["Cloudinary:ApiKey"] = "test",
                ["Cloudinary:ApiSecret"] = "test",
                ["AdminUser:Password"] = "Test1234!"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Disable antiforgery validation for integration tests
            services.AddSingleton<Microsoft.AspNetCore.Antiforgery.IAntiforgery, TestAntiforgery>();

            // Remove ALL DbContext-related registrations (Npgsql conflict)
            var dbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<RentMateContext>)
                          || d.ServiceType == typeof(RentMateContext)
                          || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in dbContextDescriptors) services.Remove(d);

            // Re-add with SQLite in-memory (supports ExecuteUpdateAsync unlike EF InMemory)
            services.AddDbContext<RentMateContext>(options =>
                options.UseSqlite(_connection)
                    .ConfigureWarnings(w => w.Ignore(
                        Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

            // Remove background services that interfere with tests
            services.RemoveAll<IHostedService>();

            // Replace payment with mock that returns success for all ops
            services.RemoveAll<IPaymentService>();
            var paymentMock = new Moq.Mock<IPaymentService>();
            paymentMock.Setup(p => p.AuthorizeAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<decimal>(), Moq.It.IsAny<string>()))
                .ReturnsAsync(PaymentResult.Succeeded("test_ref"));
            paymentMock.Setup(p => p.CaptureAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<decimal>()))
                .ReturnsAsync(PaymentResult.Succeeded("test_ref"));
            paymentMock.Setup(p => p.ReleaseAsync(Moq.It.IsAny<string>()))
                .ReturnsAsync(PaymentResult.Succeeded("test_ref"));
            paymentMock.Setup(p => p.RefundAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<decimal>()))
                .ReturnsAsync(PaymentResult.Succeeded("test_ref"));
            services.AddScoped<IPaymentService>(_ => paymentMock.Object);

            services.RemoveAll<IFileUploadService>();
            var fileUploadMock = new Moq.Mock<IFileUploadService>();
            fileUploadMock.Setup(f => f.UploadFileAsync(
                    Moq.It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                    Moq.It.IsAny<string>()))
                .ReturnsAsync("https://test.com/uploaded.jpg");
            services.AddScoped<IFileUploadService>(_ => fileUploadMock.Object);

            // Replace scoring with a no-op mock. Several controllers fire
            // fire-and-forget Task.Run(_scoringService...) calls that write to
            // the DB after the request returns; on the shared SQLite test
            // connection that races the next test's reset and produces flaky,
            // order-dependent failures. Scoring has its own unit tests.
            services.RemoveAll<IScoringService>();
            var scoringMock = new Moq.Mock<IScoringService>();
            scoringMock.Setup(s => s.ComputeAndSaveItemScoreAsync(Moq.It.IsAny<int>())).ReturnsAsync(0d);
            scoringMock.Setup(s => s.ComputeAndSaveProfileTrustScoreAsync(Moq.It.IsAny<string>())).ReturnsAsync(0d);
            scoringMock.Setup(s => s.RecordItemViewAsync(Moq.It.IsAny<int>())).Returns(Task.CompletedTask);
            scoringMock.Setup(s => s.RecordCategoryInteractionAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            scoringMock.Setup(s => s.GetPersonalizedBoostAsync(Moq.It.IsAny<string?>(), Moq.It.IsAny<string?>()))
                .ReturnsAsync(0d);
            scoringMock.Setup(s => s.DetectReviewVelocityAnomalyAsync(Moq.It.IsAny<string>())).ReturnsAsync(false);
            services.AddScoped<IScoringService>(_ => scoringMock.Object);

            // Replace auth with test scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });

    }

    private bool _dbInitialized;

    /// <summary>
    /// Drops and recreates the schema, giving the next test a clean database.
    /// Called from <see cref="IntegrationTestBase"/>'s constructor so each test
    /// is isolated even though the SQLite connection is shared for the factory
    /// lifetime. Marks the DB initialized so a following <see cref="SeedDatabase"/>
    /// only seeds (no second drop/create).
    /// </summary>
    public void ResetDatabase()
    {
        // Force app to start (triggers ConfigureWebHost)
        var _ = Server;

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RentMateContext>();

        // First reset for this factory: drop whatever schema Program.cs
        // startup applied (it runs stale migrations on the shared connection
        // that lack the newest model columns) and rebuild from the EF model.
        // This must happen on the FIRST reset, before any leaked GetDbContext
        // scope holds the shared :memory: connection — a later EnsureDeleted
        // fails with "active statements" because dropping the DB forces EF to
        // re-register SQLite user-functions on a busy connection.
        //
        // Later resets only wipe rows (schema + connection stay intact), which
        // gives per-test isolation without re-touching the connection.
        if (!_dbInitialized)
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            _dbInitialized = true;
        }

        // Delete from the tables that actually exist (read from sqlite_master),
        // not from the EF model, so a model/schema mismatch can never throw
        // "no such table" mid-reset.
        var conn = context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();

        var tables = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' " +
                "AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory';";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0));
        }

        var sql = new System.Text.StringBuilder("PRAGMA foreign_keys=OFF;");
        foreach (var table in tables)
            sql.Append($"DELETE FROM \"{table}\";");
        sql.Append("PRAGMA foreign_keys=ON;");

        context.Database.ExecuteSqlRaw(sql.ToString());
    }

    /// <summary>
    /// Seeds the database with test data. Ensures schema exists first
    /// (no-op if <see cref="ResetDatabase"/> already created it for this test).
    /// </summary>
    public void SeedDatabase(Action<RentMateContext> seed)
    {
        // Force app to start (triggers ConfigureWebHost)
        var _ = Server;

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RentMateContext>();

        if (!_dbInitialized)
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            _dbInitialized = true;
        }

        seed(context);
        context.SaveChanges();
    }

    /// <summary>
    /// Gets a scoped DbContext for assertions after HTTP calls.
    /// </summary>
    public RentMateContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<RentMateContext>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

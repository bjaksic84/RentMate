using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;

namespace RentMate.Tests.Helpers;

/// <summary>
/// Factory for creating isolated test database contexts.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates an EF Core InMemory-backed context. Each call gets a unique DB.
    /// Use for services that do NOT use ExecuteUpdateAsync.
    /// </summary>
    public static RentMateContext Create()
    {
        var options = new DbContextOptionsBuilder<RentMateContext>()
            .UseInMemoryDatabase(databaseName: $"RentMateTest_{Guid.NewGuid()}")
            .Options;
        return new RentMateContext(options);
    }

    /// <summary>
    /// Creates a SQLite in-memory-backed context. Required for services that use ExecuteUpdateAsync
    /// (ScoringService, ReviewAggregationService) since the InMemory provider doesn't support it.
    /// Caller must dispose both the context and the connection.
    /// </summary>
    public static (RentMateContext Context, SqliteConnection Connection) CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<RentMateContext>()
            .UseSqlite(connection)
            .Options;

        var context = new RentMateContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}

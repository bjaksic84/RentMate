using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests;

/// <summary>
/// Verifies that test infrastructure (InMemory DB, SQLite, EntityFactory) works correctly.
/// </summary>
public class SmokeTests
{
    [Fact]
    public async Task InMemoryContext_CanAddAndQueryEntities()
    {
        using var context = TestDbContextFactory.Create();

        var (owner, renter, item, rental, deposit) = EntityFactory.CreateFullRentalSetup();

        context.Users.AddRange(owner, renter);
        context.Items.Add(item);
        context.Rentals.Add(rental);
        if (deposit != null) context.RentalDeposits.Add(deposit);
        await context.SaveChangesAsync();

        var loaded = await context.Rentals
            .Include(r => r.Item)
            .Include(r => r.Renter)
            .Include(r => r.Owner)
            .FirstAsync(r => r.Id == rental.Id);

        Assert.Equal(RentalStatus.Active, loaded.Status);
        Assert.NotNull(loaded.Item);
        Assert.Equal("Test Item", loaded.Item!.Title);
        Assert.NotNull(loaded.Renter);
        Assert.Equal("Renter", loaded.Renter!.FirstName);
        Assert.NotNull(loaded.Owner);
        Assert.Equal("Owner", loaded.Owner!.FirstName);
    }

    [Fact]
    public async Task SqliteContext_CanAddAndQueryEntities()
    {
        var (context, connection) = TestDbContextFactory.CreateSqlite();
        try
        {
            var user = EntityFactory.CreateUser(firstName: "SqliteUser");
            var item = EntityFactory.CreateItem(userId: user.Id, price: 25m);

            context.Users.Add(user);
            context.Items.Add(item);
            await context.SaveChangesAsync();

            var loaded = await context.Items.FirstAsync(i => i.Id == item.Id);
            Assert.Equal(25m, loaded.Price);
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }
}

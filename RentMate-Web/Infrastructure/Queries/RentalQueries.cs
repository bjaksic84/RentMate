using Microsoft.EntityFrameworkCore;
using RentMate.Models.Domain;

namespace RentMate.Infrastructure.Queries;

/// <summary>
/// Reusable EF Core Include chains for Rental queries.
/// Eliminates repeated navigation-property loads across services and controllers.
/// </summary>
public static class RentalQueries
{
    /// <summary>
    /// Includes Item, Renter, and Owner. Use for dashboards, admin lists, and any
    /// view that needs to display rental participants alongside the item title.
    /// </summary>
    public static IQueryable<Rental> WithAllParties(this IQueryable<Rental> query)
        => query
            .Include(r => r.Item)
            .Include(r => r.Renter)
            .Include(r => r.Owner);

    /// <summary>
    /// Extends WithAllParties with the full dispute chain:
    /// Deposit → Evidence → SubmittedBy. Use for dispute history and admin review.
    /// </summary>
    public static IQueryable<Rental> WithDisputeDetails(this IQueryable<Rental> query)
        => query
            .Include(r => r.Item)
            .Include(r => r.Renter)
            .Include(r => r.Owner)
            .Include(r => r.Deposit).ThenInclude(d => d!.Evidence).ThenInclude(e => e.SubmittedBy);
}

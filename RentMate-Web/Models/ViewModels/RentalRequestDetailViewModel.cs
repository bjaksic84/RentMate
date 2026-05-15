using RentMate.Models.Domain;

namespace RentMate.Models.ViewModels;

/// <summary>
/// Enriched data for a single pending rental request, used to populate
/// the owner's review modal on the Lending dashboard.
/// </summary>
public class RentalRequestDetailViewModel
{
    #region Renter reputation

    /// <summary>Average star rating this person has received as a renter.</summary>
    public double RenterAvgRating { get; set; }

    /// <summary>Number of reviews this person has received as a renter.</summary>
    public int RenterReviewCount { get; set; }

    /// <summary>Completed rentals count for this renter.</summary>
    public int RenterCompletedRentalsCount { get; set; }

    #endregion

    #region Booking economics

    /// <summary>Base price: item daily price × days.</summary>
    public decimal BasePrice { get; set; }

    /// <summary>Total cost of accessories selected by the renter.</summary>
    public decimal AccessoriesTotal { get; set; }

    /// <summary>Deposit amount held during the rental.</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>Total payout the owner receives (BasePrice + AccessoriesTotal).</summary>
    public decimal NetPayout { get; set; }

    #endregion

    #region Calendar conflicts

    /// <summary>
    /// Accepted or active rentals on the same item whose date range overlaps
    /// with this pending request. Empty list means no conflicts.
    /// </summary>
    public List<Rental> ConflictingRentals { get; set; } = new();

    /// <summary>True if any accepted/active rental overlaps this request's dates.</summary>
    public bool HasConflict => ConflictingRentals.Count > 0;

    #endregion
}

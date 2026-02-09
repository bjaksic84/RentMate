using RentMate.Models.Domain;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Models.ViewModels;

/// <summary>
/// View model for the RentalStatusCard view component.
/// Adapts rental data for display with computed properties.
/// </summary>
public class RentalCardViewModel
{
    public int RentalId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public string? ItemImageUrl { get; set; }
    public int ItemId { get; set; }
    public decimal DailyRate { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public RentalStatus Status { get; set; }

    #region Deposit

    public decimal? DepositAmount { get; set; }
    public DepositStatus? DepositStatus { get; set; }

    #endregion

    #region Accessories

    public List<RentalAccessoryViewModel> Accessories { get; set; } = new();

    #endregion

    #region Extensions

    public bool HasPendingExtension { get; set; }
    public bool CanRequestExtension { get; set; }
    public bool AutoApproveExtensions { get; set; }
    public DateTime? EarliestConflictDate { get; set; }

    #endregion

    #region Context

    public bool IsOwnerView { get; set; }
    public string? RenterName { get; set; }
    public string? RenterProfileImage { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerProfileImage { get; set; }

    #endregion

    #region Review Context

    public bool HasReview { get; set; }
    public int? ExistingReviewId { get; set; }

    #endregion

    #region Computed Properties

    public int DaysRemaining => (EndDate.Date - DateTime.UtcNow.Date).Days;

    public bool IsOverdue => Status == RentalStatus.Active && DaysRemaining < 0;

    public string CountdownUrgency => DaysRemaining switch
    {
        < 0 => "overdue",
        <= 1 => "urgent",
        <= 3 => "warning",
        _ => "normal"
    };

    #endregion
}

/// <summary>
/// View model for displaying a rental accessory.
/// </summary>
public class RentalAccessoryViewModel
{
    public string Name { get; set; } = string.Empty;
    public decimal DailyPrice { get; set; }
}

using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Models.ViewModels;

/// <summary>
/// Typed model for _RenterRentalRow and _OwnerRentalRow partials.
/// Replaces the anonymous-type + reflection pattern used by their callers.
/// </summary>
public class RentalRowViewModel
{
    public required Rental Rental { get; init; }
    public required string CurrentUserId { get; init; }
    public required ICurrencyService CurrencyService { get; init; }
}

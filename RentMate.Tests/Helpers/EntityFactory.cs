using RentMate.Models.Domain;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Helpers;

/// <summary>
/// Static factory methods for building domain entities with sensible defaults.
/// Navigation properties are set explicitly because EF Core InMemory has no lazy loading.
/// </summary>
public static class EntityFactory
{
    private static int _nextId = 1000;
    private static int NextId() => Interlocked.Increment(ref _nextId);

    #region ApplicationUser

    public static ApplicationUser CreateUser(
        string? id = null,
        string? firstName = "Test",
        string? lastName = "User",
        string? email = null,
        bool emailConfirmed = true,
        string? city = "Ljubljana",
        bool isPhoneVerified = false,
        bool isGovernmentIdVerified = false,
        bool isSocialMediaLinked = false,
        bool hasPaymentMethodAdded = false,
        string? profilePictureUrl = null,
        string? bio = null,
        bool onboardingCompleted = true,
        double responseRate = 0,
        double avgResponseTimeHours = 0,
        int totalMessagesReceived = 0,
        double profileTrustScore = 0,
        string? categoryAffinityJson = null,
        bool hasReturnPolicy = false,
        DateTime? createdAt = null,
        UserIntent? userIntent = null,
        bool spotlightTourCompleted = false,
        bool isDeactivated = false,
        DateTime? deactivatedAt = null,
        DeactivationSource? deactivatedBy = null,
        string? deactivationReason = null,
        string preferredLanguage = "",
        bool notifyOnMessage = false,
        bool notifyOnRentalRequest = false,
        bool notifyOnRentalStatusChange = false,
        bool notifyOnReview = false,
        double? latitude = null,
        double? longitude = null)
    {
        var userId = id ?? Guid.NewGuid().ToString();
        return new ApplicationUser
        {
            Id = userId,
            UserName = email ?? $"user_{userId[..8]}@test.com",
            Email = email ?? $"user_{userId[..8]}@test.com",
            NormalizedEmail = (email ?? $"user_{userId[..8]}@test.com").ToUpperInvariant(),
            NormalizedUserName = (email ?? $"user_{userId[..8]}@test.com").ToUpperInvariant(),
            EmailConfirmed = emailConfirmed,
            FirstName = firstName,
            LastName = lastName,
            City = city,
            IsPhoneVerified = isPhoneVerified,
            IsGovernmentIdVerified = isGovernmentIdVerified,
            IsSocialMediaLinked = isSocialMediaLinked,
            HasPaymentMethodAdded = hasPaymentMethodAdded,
            ProfilePictureUrl = profilePictureUrl,
            Bio = bio,
            OnboardingCompleted = onboardingCompleted,
            ResponseRate = responseRate,
            AvgResponseTimeHours = avgResponseTimeHours,
            TotalMessagesReceived = totalMessagesReceived,
            ProfileTrustScore = profileTrustScore,
            CategoryAffinityJson = categoryAffinityJson,
            HasReturnPolicy = hasReturnPolicy,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(),
            UserIntent = userIntent,
            SpotlightTourCompleted = spotlightTourCompleted,
            IsDeactivated = isDeactivated,
            DeactivatedAt = deactivatedAt,
            DeactivatedBy = deactivatedBy,
            DeactivationReason = deactivationReason,
            PreferredLanguage = preferredLanguage,
            NotifyOnMessage = notifyOnMessage,
            NotifyOnRentalRequest = notifyOnRentalRequest,
            NotifyOnRentalStatusChange = notifyOnRentalStatusChange,
            NotifyOnReview = notifyOnReview,
            Latitude = latitude,
            Longitude = longitude
        };
    }

    #endregion

    #region Item

    public static Item CreateItem(
        int? id = null,
        string? userId = null,
        decimal price = 10.00m,
        string title = "Test Item",
        string? description = "A test item description",
        string category = "Electronics",
        bool isListed = true,
        bool isRented = false,
        bool isAdminHidden = false,
        decimal? depositAmount = null,
        bool autoApproveExtensions = false,
        int? maxRentalDays = null,
        string? condition = "Good",
        double? averageRating = null,
        int reviewCount = 0,
        int viewsLast30Days = 0,
        double itemScore = 0,
        DateTime? createdAt = null,
        double? latitude = null,
        double? longitude = null,
        string? location = "Ljubljana")
    {
        return new Item
        {
            Id = id ?? NextId(),
            UserId = userId,
            Price = price,
            Title = title,
            Description = description,
            Category = category,
            IsListed = isListed,
            IsRented = isRented,
            IsAdminHidden = isAdminHidden,
            DepositAmount = depositAmount,
            AutoApproveExtensions = autoApproveExtensions,
            MaxRentalDays = maxRentalDays,
            Condition = condition,
            AverageRating = averageRating,
            ReviewCount = reviewCount,
            ViewsLast30Days = viewsLast30Days,
            ItemScore = itemScore,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow,
            Location = location,
            Latitude = latitude,
            Longitude = longitude
        };
    }

    #endregion

    #region Rental

    /// <summary>
    /// Creates a rental with navigation properties wired up.
    /// If item/owner/renter are provided, they'll be set as nav props.
    /// </summary>
    public static Rental CreateRental(
        int? id = null,
        int itemId = 0,
        string renterId = "",
        string? ownerId = null,
        RentalStatus status = RentalStatus.Active,
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal totalPrice = 100m,
        DateTime? createdAt = null,
        DateTime? archivedAt = null,
        Item? item = null,
        ApplicationUser? renter = null,
        ApplicationUser? owner = null)
    {
        var rental = new Rental
        {
            Id = id ?? NextId(),
            ItemId = item?.Id ?? itemId,
            RenterId = renter?.Id ?? renterId,
            OwnerId = owner?.Id ?? ownerId,
            Status = status,
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-5),
            EndDate = endDate ?? DateTime.UtcNow.AddDays(5),
            RentalDate = createdAt ?? DateTime.UtcNow,
            TotalPrice = totalPrice,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ArchivedAt = archivedAt
        };

        if (item != null) rental.Item = item;
        if (renter != null) rental.Renter = renter;
        if (owner != null) rental.Owner = owner;

        return rental;
    }

    #endregion

    #region RentalDeposit

    public static RentalDeposit CreateDeposit(
        int? id = null,
        int rentalId = 0,
        decimal amount = 100m,
        DepositStatus status = DepositStatus.Authorized,
        decimal? chargedAmount = null,
        string? chargeReason = null,
        string? paymentReference = "pay_ref_test",
        string? disputeReason = null,
        DateTime? disputeDeadline = null,
        decimal? counterOfferAmount = null,
        string? ownerDisputeResponse = null,
        DateTime? escalatedAt = null,
        string? adminNotes = null,
        string? adminResolvedByUserId = null,
        DateTime? adminResolvedAt = null,
        DateTime? chargeAcceptedAt = null,
        int disputeRoundCount = 0,
        Rental? rental = null)
    {
        var deposit = new RentalDeposit
        {
            Id = id ?? NextId(),
            RentalId = rental?.Id ?? rentalId,
            Amount = amount,
            Status = status,
            ChargedAmount = chargedAmount,
            ChargeReason = chargeReason,
            PaymentReference = paymentReference,
            DisputeReason = disputeReason,
            DisputeDeadline = disputeDeadline,
            CounterOfferAmount = counterOfferAmount,
            OwnerDisputeResponse = ownerDisputeResponse,
            EscalatedAt = escalatedAt,
            AdminNotes = adminNotes,
            AdminResolvedByUserId = adminResolvedByUserId,
            AdminResolvedAt = adminResolvedAt,
            ChargeAcceptedAt = chargeAcceptedAt,
            DisputeRoundCount = disputeRoundCount,
            AuthorizedAt = status >= DepositStatus.Authorized ? DateTime.UtcNow : null
        };

        if (rental != null) deposit.Rental = rental;

        return deposit;
    }

    #endregion

    #region RentalExtension

    public static RentalExtension CreateExtension(
        int? id = null,
        int rentalId = 0,
        string requestedByUserId = "",
        DateTime? originalEndDate = null,
        DateTime? newEndDate = null,
        ExtensionStatus status = ExtensionStatus.Pending,
        decimal dailyRate = 10m,
        decimal additionalCost = 50m,
        Rental? rental = null)
    {
        var ext = new RentalExtension
        {
            Id = id ?? NextId(),
            RentalId = rental?.Id ?? rentalId,
            RequestedByUserId = requestedByUserId,
            OriginalEndDate = originalEndDate ?? DateTime.UtcNow.AddDays(5),
            NewEndDate = newEndDate ?? DateTime.UtcNow.AddDays(10),
            Status = status,
            DailyRate = dailyRate,
            AdditionalCost = additionalCost
        };

        if (rental != null) ext.Rental = rental;

        return ext;
    }

    #endregion

    #region Review

    public static Review CreateReview(
        int? id = null,
        int itemId = 0,
        string? reviewerId = null,
        int rating = 4,
        string? title = "Good item",
        string? body = "Works as described.",
        bool isAnonymous = false,
        bool isDeleted = false,
        int? rentalId = null,
        DateTime? createdAt = null)
    {
        return new Review
        {
            Id = id ?? NextId(),
            ItemId = itemId,
            ReviewerId = reviewerId,
            Rating = rating,
            Title = title,
            Body = body,
            IsAnonymous = isAnonymous,
            IsDeleted = isDeleted,
            RentalId = rentalId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    #endregion

    #region ItemAccessory

    public static ItemAccessory CreateAccessory(
        int? id = null,
        int itemId = 0,
        string name = "Test Accessory",
        decimal dailyPrice = 5m,
        string? description = null,
        bool isAvailable = true)
    {
        return new ItemAccessory
        {
            Id = id ?? NextId(),
            ItemId = itemId,
            Name = name,
            DailyPrice = dailyPrice,
            Description = description,
            IsAvailable = isAvailable
        };
    }

    #endregion

    #region ItemImage

    public static ItemImage CreateItemImage(
        int? id = null,
        int itemId = 0,
        string imageUrl = "https://test.com/image.jpg",
        int displayOrder = 0)
    {
        return new ItemImage
        {
            Id = id ?? NextId(),
            ItemId = itemId,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder
        };
    }

    #endregion

    #region DisputeEvidence

    public static DisputeEvidence CreateEvidence(
        int? id = null,
        int rentalDepositId = 0,
        string submittedByUserId = "",
        string url = "https://test.com/evidence.jpg",
        string? notes = null)
    {
        return new DisputeEvidence
        {
            Id = id ?? NextId(),
            RentalDepositId = rentalDepositId,
            SubmittedByUserId = submittedByUserId,
            Url = url,
            Notes = notes
        };
    }

    #endregion

    #region Convenience: Full Rental Setup

    /// <summary>
    /// Creates a complete rental scenario: owner, renter, item, rental, and optionally a deposit.
    /// All navigation properties wired. Add all returned entities to the context.
    /// </summary>
    public static (ApplicationUser Owner, ApplicationUser Renter, Item Item, Rental Rental, RentalDeposit? Deposit)
        CreateFullRentalSetup(
            decimal itemPrice = 20m,
            decimal? depositAmount = 100m,
            RentalStatus rentalStatus = RentalStatus.Active,
            DepositStatus? depositStatus = DepositStatus.Authorized)
    {
        var owner = CreateUser(firstName: "Owner", lastName: "Smith");
        var renter = CreateUser(firstName: "Renter", lastName: "Jones");
        var item = CreateItem(userId: owner.Id, price: itemPrice, depositAmount: depositAmount);
        item.User = owner;

        var rental = CreateRental(
            item: item,
            renter: renter,
            owner: owner,
            status: rentalStatus,
            totalPrice: itemPrice * 10);

        RentalDeposit? deposit = null;
        if (depositAmount.HasValue && depositStatus.HasValue)
        {
            deposit = CreateDeposit(
                rental: rental,
                amount: depositAmount.Value,
                status: depositStatus.Value);
        }

        return (owner, renter, item, rental, deposit);
    }

    #endregion
}

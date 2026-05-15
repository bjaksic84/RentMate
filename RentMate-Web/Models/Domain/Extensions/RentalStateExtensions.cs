using RentMate.Shared.Contracts.Responses;

namespace RentMate.Models.Domain.Extensions
{
    /// <summary>
    /// Extension methods for rental and deposit state checks.
    /// Centralises repeated compound status predicates used across controllers and views.
    /// </summary>
    public static class RentalStateExtensions
    {
        #region Rental extensions

        /// <summary>
        /// Active rental whose end date has passed (renter has not returned the item yet).
        /// </summary>
        public static bool IsOverdue(this Rental rental)
            => rental.Status == RentalStatus.Active && rental.EndDate.Date < DateTime.UtcNow.Date;

        /// <summary>
        /// Rental is either active or pending (occupies calendar / blocks new bookings).
        /// </summary>
        public static bool IsActiveOrPending(this Rental rental)
            => rental.Status == RentalStatus.Active || rental.Status == RentalStatus.Pending;

        /// <summary>
        /// Rental is completed and has not been archived yet (still in the active tab grace period).
        /// </summary>
        public static bool IsArchivable(this Rental rental)
            => rental.Status == RentalStatus.Completed && rental.ArchivedAt == null;

        #endregion

        #region RentalDeposit extensions

        /// <summary>
        /// Deposit has been charged (full or partial).
        /// </summary>
        public static bool IsCharged(this RentalDeposit deposit)
            => deposit.Status == DepositStatus.Charged || deposit.Status == DepositStatus.PartiallyCharged;

        /// <summary>
        /// Deposit is in an active dispute state (disputed, counter-offered, or escalated to admin).
        /// </summary>
        public static bool IsInDispute(this RentalDeposit deposit)
            => deposit.Status == DepositStatus.Disputed
            || deposit.Status == DepositStatus.CounterOffered
            || deposit.Status == DepositStatus.Escalated;

        /// <summary>
        /// Renter needs to take action: deposit was charged (full/partial) or a counter-offer is waiting.
        /// </summary>
        public static bool NeedsRenterAction(this RentalDeposit deposit)
            => deposit.Status == DepositStatus.Charged
            || deposit.Status == DepositStatus.PartiallyCharged
            || deposit.Status == DepositStatus.CounterOffered;

        /// <summary>
        /// Owner needs to take action: renter filed a dispute.
        /// </summary>
        public static bool NeedsOwnerAction(this RentalDeposit deposit)
            => deposit.Status == DepositStatus.Disputed;

        /// <summary>
        /// Deposit is in any non-terminal active dispute (owner must respond or admin is reviewing).
        /// Excludes Charged/PartiallyCharged/Released/ChargeUpheld which are terminal.
        /// </summary>
        public static bool HasOpenDispute(this RentalDeposit deposit)
            => deposit.Status == DepositStatus.Disputed
            || deposit.Status == DepositStatus.CounterOffered
            || deposit.Status == DepositStatus.Escalated;

        #endregion

        #region Nullable helpers (for rentals where Deposit may be null)

        /// <summary>
        /// Returns true if the rental has no deposit or the deposit is not in an active dispute.
        /// Used for "completed and clean" filtering in dashboard.
        /// </summary>
        public static bool HasNoActiveDispute(this Rental rental)
            => rental.Deposit == null || !rental.Deposit.IsInDispute();

        #endregion
    }
}

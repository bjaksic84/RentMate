namespace RentMate.Models.Dto
{
    /// <summary>
    /// Aggregated attention counts for the navbar badge and dashboard tabs.
    /// Computed once per request by IAttentionCountService.
    /// </summary>
    public record AttentionCounts(
        int Overdue,
        int Accepted,
        int PendingRequests,
        int PendingExtensions,
        int RenterDepositAction,
        int OwnerDisputedDeposits,
        int ExtensionPayments,
        int CompletedArchivable,
        int AdminEscalated)
    {
        /// <summary>
        /// Total count shown in the navbar badge (user-facing items only, excludes admin).
        /// </summary>
        public int Total => Overdue + Accepted + PendingRequests + PendingExtensions
            + RenterDepositAction + OwnerDisputedDeposits + ExtensionPayments + CompletedArchivable;

        public static AttentionCounts Empty => new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}

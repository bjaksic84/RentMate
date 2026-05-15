using RentMate.Models.Dto;

namespace RentMate.Services.Interfaces
{
    /// <summary>
    /// Computes dashboard attention counts for a user in a single call.
    /// Used by the navbar to display the badge count without 8 separate inline queries.
    /// </summary>
    public interface IAttentionCountService
    {
        /// <summary>
        /// Returns attention counts for the given user. Also includes admin escalated count
        /// (non-zero only when the user is an admin).
        /// </summary>
        Task<AttentionCounts> GetForUserAsync(string userId, bool isAdmin = false);
    }
}

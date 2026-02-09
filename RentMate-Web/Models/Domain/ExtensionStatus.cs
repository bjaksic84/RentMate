namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents the status of a rental extension request.
    /// </summary>
    public enum ExtensionStatus
    {
        Pending = 0,
        Approved = 1,
        Declined = 2,
        AutoApproved = 3
    }
}

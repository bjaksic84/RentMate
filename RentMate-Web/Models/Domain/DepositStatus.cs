namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents the lifecycle status of a rental deposit authorization.
    /// </summary>
    public enum DepositStatus
    {
        Pending = 0,
        Authorized = 1,
        Released = 2,
        Charged = 3,
        PartiallyCharged = 4
    }
}

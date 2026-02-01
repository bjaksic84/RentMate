namespace RentMate.Models;

/// <summary>
/// Join entity representing a user's favorite item.
/// Uses a composite primary key (AccountId, ItemId).
/// </summary>
public class AccountItemFavorite
{
    /// <summary>
    /// Foreign key to the user who favorited the item.
    /// </summary>
    public string AccountId { get; set; } = null!;

    /// <summary>
    /// Foreign key to the favorited item.
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Timestamp when the favorite was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser Account { get; set; } = null!;
    public virtual Item Item { get; set; } = null!;
}

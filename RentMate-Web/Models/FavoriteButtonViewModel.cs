namespace RentMate.Models;

/// <summary>
/// ViewModel for the _FavoriteButton partial view.
/// </summary>
public class FavoriteButtonViewModel
{
    /// <summary>
    /// The ID of the item to favorite/unfavorite.
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Whether the current user has favorited this item.
    /// </summary>
    public bool IsFavorited { get; set; }

    /// <summary>
    /// Size of the heart icon: "sm", "md", or "lg".
    /// Default is "md".
    /// </summary>
    public string Size { get; set; } = "md";
}

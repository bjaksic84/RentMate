namespace RentMate.Models.ViewModels;

/// <summary>
/// ViewModel for displaying owner information in a modal dialog.
/// </summary>
public class OwnerModalViewModel
{
    /// <summary>User's unique identifier.</summary>
    public string? Id { get; set; }

    /// <summary>User's first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>User's last name.</summary>
    public string? LastName { get; set; }

    /// <summary>User's city/location.</summary>
    public string? City { get; set; }

    /// <summary>User's email address.</summary>
    public string? Email { get; set; }

    /// <summary>URL to user's profile picture.</summary>
    public string? ProfilePictureUrl { get; set; }

    /// <summary>Average rating across all user's items.</summary>
    public double AverageRating { get; set; }

    /// <summary>Total number of reviews across all items.</summary>
    public int ReviewCount { get; set; }

    /// <summary>Date when the user joined the platform.</summary>
    public DateTime JoinDate { get; set; }
}

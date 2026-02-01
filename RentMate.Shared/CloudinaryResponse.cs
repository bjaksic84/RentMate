namespace RentMate.Shared;

/// <summary>
/// Response model for Cloudinary upload API.
/// Maps to JSON response from Cloudinary's upload endpoint.
/// </summary>
public class CloudinaryResponse
{
    /// <summary>Unique identifier for the uploaded resource.</summary>
    public string? public_id { get; set; }

    /// <summary>HTTPS URL to access the uploaded file (store this in database).</summary>
    public string? secure_url { get; set; }

    /// <summary>File format/extension (e.g., "jpg", "png").</summary>
    public string? format { get; set; }

    /// <summary>Image width in pixels.</summary>
    public int width { get; set; }

    /// <summary>Image height in pixels.</summary>
    public int height { get; set; }
}
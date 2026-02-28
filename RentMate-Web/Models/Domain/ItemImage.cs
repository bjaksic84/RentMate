using System.ComponentModel.DataAnnotations;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents an image associated with an Item.
    /// Supports multiple images per item with ordering.
    /// </summary>
    public class ItemImage
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the parent Item.
        /// </summary>
        [Required]
        public int ItemId { get; set; }

        /// <summary>
        /// The Cloudinary URL for this image.
        /// </summary>
        [Required]
        [Url]
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Display order of the image. Lower numbers appear first.
        /// The image with DisplayOrder = 0 is considered the primary/main image.
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// When the image was uploaded.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual Item? Item { get; set; }
    }
}

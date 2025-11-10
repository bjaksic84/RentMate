using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RentMate.Models
{
    public class Review
    {
        public int Id { get; set; }

        // FK to Item
        public int ItemId { get; set; }
        [JsonIgnore]
        public Item? Item { get; set; }

        // FK to Reviewer

        public string? ReviewerId { get; set; } = null!;
        [JsonIgnore]
        public ApplicationUser? Reviewer { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Body { get; set; }

        public bool IsAnonymous { get; set; } = false;
        public bool IsDeleted { get; set; } = false;

        // Optional link to Rental that allowed this review
        public int? RentalId { get; set; }
        public Rental? Rental { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}

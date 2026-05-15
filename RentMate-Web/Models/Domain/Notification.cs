using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain;

/// <summary>
/// A persistent notification delivered to a user.
/// </summary>
public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Required]
    public NotificationType Type { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = default!;

    [MaxLength(500)]
    public string? Message { get; set; }

    public int? ReferenceId { get; set; }

    [MaxLength(50)]
    public string? ReferenceType { get; set; }

    [MaxLength(500)]
    public string? ActionUrl { get; set; }

    public bool IsRead { get; set; }

    public bool IsDismissed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }
}

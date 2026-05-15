using RentMate.Models.Domain;

namespace RentMate.Models.Dto;

/// <summary>
/// Groups duplicate notifications (same type + reference) for collapsed dropdown display.
/// </summary>
public class GroupedNotification
{
    public Notification LatestNotification { get; set; } = default!;
    public int Count { get; set; }
    public List<int> Ids { get; set; } = [];
}

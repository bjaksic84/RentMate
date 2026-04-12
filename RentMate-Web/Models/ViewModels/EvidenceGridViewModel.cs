using RentMate.Models.Domain;

namespace RentMate.Models.ViewModels;

/// <summary>
/// View model for the _EvidenceGrid partial. Renders two evidence thumbnail
/// grids (other party's + yours) for a deposit dispute panel.
/// </summary>
public class EvidenceGridViewModel
{
    public IEnumerable<DisputeEvidence> Evidence { get; init; } = [];
    public string CurrentUserId { get; init; } = string.Empty;

    /// <summary>Label shown above the other party's evidence grid.</summary>
    public string OtherPartyLabel { get; init; } = string.Empty;

    /// <summary>Tailwind border/text color token for the section divider and label (e.g. "rose").</summary>
    public string ColorToken { get; init; } = "rose";

    /// <summary>Tailwind size classes for thumbnails, e.g. "w-10 h-10" or "w-8 h-8".</summary>
    public string ThumbnailSize { get; init; } = "w-10 h-10";

    /// <summary>Rental ID used by openAddEvidenceModal JS call. Null disables the add-evidence button.</summary>
    public int? RentalId { get; init; }
}

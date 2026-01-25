namespace RentMate.Shared.Contracts.Requests;

/// <summary>
/// Request to send a message.
/// </summary>
public record SendMessageRequest(
    string RecipientId,
    string Content,
    int? RelatedItemId = null
);

/// <summary>
/// Request to mark notifications as read.
/// </summary>
public record MarkNotificationsReadRequest(
    List<int>? NotificationIds = null,
    bool MarkAllRead = false
);

/// <summary>
/// Request to update notification preferences.
/// </summary>
public record NotificationPreferencesRequest
{
    public bool EmailNotifications { get; init; } = true;
    public bool PushNotifications { get; init; } = true;
    public bool RentalUpdates { get; init; } = true;
    public bool NewReviews { get; init; } = true;
    public bool PaymentAlerts { get; init; } = true;
    public bool MarketingEmails { get; init; } = false;
}

/// <summary>
/// Admin request to hide/unhide an item.
/// </summary>
public record AdminItemActionRequest(
    int ItemId,
    bool Hide,
    string? Reason = null
);

/// <summary>
/// Admin request to manage user.
/// </summary>
public record AdminUserActionRequest
{
    public string UserId { get; init; } = string.Empty;
    public AdminUserAction Action { get; init; }
    public string? Reason { get; init; }
    public List<string>? RolesToAdd { get; init; }
    public List<string>? RolesToRemove { get; init; }
}

/// <summary>
/// Available admin actions on users.
/// </summary>
public enum AdminUserAction
{
    /// <summary>Temporarily suspend the user.</summary>
    Suspend,
    
    /// <summary>Reactivate a suspended user.</summary>
    Reactivate,
    
    /// <summary>Permanently ban the user.</summary>
    Ban,
    
    /// <summary>Update user roles.</summary>
    UpdateRoles,
    
    /// <summary>Reset user's password (sends email).</summary>
    ResetPassword
}

/// <summary>
/// Request to get messages in a thread.
/// </summary>
public record GetMessagesRequest
{
    public string ThreadId { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public DateTime? Before { get; init; }
}

/// <summary>
/// Refresh token request.
/// </summary>
public record RefreshTokenRequest(
    string RefreshToken
);

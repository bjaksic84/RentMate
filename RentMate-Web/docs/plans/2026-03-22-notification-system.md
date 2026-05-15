# Notification System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a persistent, database-backed notification system with bell icon, dropdown UI, auto-dismiss on resolution, and real-time SignalR push — so users never miss important events.

**Architecture:** New `Notification` entity stored in PostgreSQL, managed by `INotificationService`. Every existing SignalR send point also creates a persistent notification. A bell icon in the navbar shows unread count and a dropdown with recent notifications. Clicking navigates to the relevant page; X dismisses. Auto-dismiss clears notifications when the underlying issue resolves.

**Tech Stack:** ASP.NET Core MVC, EF Core + PostgreSQL, SignalR, vanilla JS, Tailwind CSS (CDN), Bootstrap Icons

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `Models/Domain/Notification.cs` | Entity class |
| `Models/Domain/NotificationType.cs` | Enum with all notification categories |
| `Models/Domain/GroupedNotification.cs` | DTO for collapsed duplicate notifications |
| `Services/Interfaces/INotificationService.cs` | Service interface |
| `Services/Implementations/NotificationService.cs` | Implementation: CRUD + SignalR push + auto-dismiss + grouping |
| `Controllers/Mvc/NotificationController.cs` | MVC endpoints for bell UI (unread count, recent grouped, dismiss, mark read) |
| `Views/Shared/_NotificationBell.cshtml` | Bell icon + desktop dropdown + mobile slide-up panel |
| `wwwroot/js/notifications.js` | Client-side: fetch, render, dismiss, sound, mobile panel, SignalR listeners |
| `wwwroot/sounds/notification.mp3` | Short notification chime sound (~0.5s, <10KB, CC0-licensed) |

### Modified Files

| File | Change |
|------|--------|
| `Infrastructure/Data/RentMateContext.cs` | Add DbSet + entity config + indexes |
| `Hubs/RentMateHub.cs` | Add `NewNotificationEvent`, `NotificationDismissedEvent` constants |
| `Program.cs` | Register `INotificationService` |
| `Views/Shared/_NavBar.cshtml` | Insert bell partial before profile avatar |
| `Views/Shared/_Layout.cshtml` | Load `notifications.js` |
| `wwwroot/js/layout.js` | Add `NewNotification` / `NotificationDismissed` SignalR listeners for badge |
| `Controllers/Mvc/RentalsController.cs` | Add `_notificationService` DI + CreateAsync calls |
| `Controllers/Mvc/DashboardController.cs` | Add `_notificationService` DI + CreateAsync + AutoDismiss calls |
| `Controllers/Mvc/DisputeController.cs` | Add `_notificationService` DI + CreateAsync + AutoDismiss calls |
| `Controllers/Mvc/PaymentController.cs` | Add `_notificationService` DI + CreateAsync call |
| `Services/Implementations/OverdueRentalService.cs` | Add `INotificationService` + CreateAsync calls |
| `Services/Implementations/DataRetentionService.cs` | Add notification cleanup method |
| `Controllers/Mvc/ReviewsController.cs` | Add `_notificationService` DI + ReviewReceived notification |
| `Resources/en.json` | ~30 new localization keys |
| `Resources/sl.json` | ~30 new localization keys |

---

## Task 1: Entity & Enum

**Files:**
- Create: `Models/Domain/NotificationType.cs`
- Create: `Models/Domain/Notification.cs`

- [ ] **Step 1: Create `NotificationType.cs`**

```csharp
namespace RentMate.Models.Domain;

/// <summary>
/// Categories of persistent notifications.
/// </summary>
public enum NotificationType
{
    // Rental lifecycle
    RentalRequested,
    RentalAccepted,
    RentalApproved,
    RentalCancelled,
    RentalCompleted,
    RentalOverdue,

    // Extensions
    ExtensionRequested,
    ExtensionApproved,
    ExtensionAutoApproved,
    ExtensionDeclined,
    ExtensionCancelled,
    ExtensionPaid,

    // Deposits & disputes
    DepositCharged,
    DepositReleased,
    DepositDisputed,
    DepositCounterOffered,
    DepositEscalated,
    DepositResolved,
    DeadlineAutoResolved,

    // Social
    ReviewReceived,

    // Admin
    AdminItemHidden,
    AdminDisputeResolved,

    // Payments
    PaymentSucceeded,
    PaymentFailed,
    PaymentRefunded,

    // Account & security
    AccountDeactivationWarning,
    AccountReactivated,
    SecurityAlert
}
```

- [ ] **Step 2: Create `Notification.cs`**

```csharp
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
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build RentMate.sln`
Expected: Build succeeded, 0 errors

---

## Task 2: DbContext & Migration

**Files:**
- Modify: `Infrastructure/Data/RentMateContext.cs`

- [ ] **Step 1: Add DbSet after line 33 (CookieConsents)**

```csharp
public DbSet<Notification> Notifications { get; set; }
```

- [ ] **Step 2: Add `ConfigureNotificationEntity` call in `OnModelCreating` after `ConfigureCookieConsentEntity` (line 54)**

```csharp
ConfigureNotificationEntity(modelBuilder);
```

- [ ] **Step 3: Add configuration method (add before `#endregion` closing Entity Configurations region)**

```csharp
private static void ConfigureNotificationEntity(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Notification>(entity =>
    {
        entity.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(n => n.Title).HasMaxLength(200);
        entity.Property(n => n.Message).HasMaxLength(500);
        entity.Property(n => n.ReferenceType).HasMaxLength(50);
        entity.Property(n => n.ActionUrl).HasMaxLength(500);

        entity.HasIndex(n => new { n.UserId, n.IsRead, n.IsDismissed });
        entity.HasIndex(n => new { n.ReferenceId, n.ReferenceType });
        entity.HasIndex(n => new { n.UserId, n.CreatedAt })
            .IsDescending(false, true);
    });
}
```

- [ ] **Step 4: Create migration**

Run: `dotnet ef migrations add AddNotifications --project RentMate-Web/RentMate.csproj`
Expected: Migration file created successfully

- [ ] **Step 5: Apply migration**

Run: `dotnet ef database update --project RentMate-Web/RentMate.csproj`
Expected: Database updated successfully

---

## Task 3: SignalR Hub Constants

**Files:**
- Modify: `Hubs/RentMateHub.cs`

- [ ] **Step 1: Add two new event constants after `RentalOverdueEvent` (line 46), inside the `#region Constants`**

```csharp
/// <summary>
/// Client method name for new persistent notification events.
/// </summary>
public const string NewNotificationEvent = "NewNotification";

/// <summary>
/// Client method name for auto-dismissed notification events.
/// </summary>
public const string NotificationDismissedEvent = "NotificationDismissed";
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build RentMate.sln`
Expected: Build succeeded

---

## Task 4: Service Interface & Implementation

**Files:**
- Create: `Services/Interfaces/INotificationService.cs`
- Create: `Services/Implementations/NotificationService.cs`

- [ ] **Step 1: Create `INotificationService.cs`**

```csharp
using RentMate.Models.Domain;

namespace RentMate.Services.Interfaces;

/// <summary>
/// Manages persistent notification lifecycle: creation, retrieval, dismissal, and auto-resolution.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates a notification, saves to DB, and pushes via SignalR.
    /// </summary>
    Task CreateAsync(string userId, NotificationType type, string title,
        string? message = null, int? referenceId = null,
        string? referenceType = null, string? actionUrl = null);

    /// <summary>
    /// Returns count of unread, non-dismissed notifications for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>
    /// Returns recent non-dismissed notifications for a user, newest first.
    /// </summary>
    Task<List<Notification>> GetRecentAsync(string userId, int limit = 20);

    /// <summary>
    /// Returns recent notifications grouped by (ReferenceId, ReferenceType, Type) to collapse duplicates.
    /// </summary>
    Task<List<GroupedNotification>> GetRecentGroupedAsync(string userId, int limit = 20);

    /// <summary>
    /// Marks a single notification as read.
    /// </summary>
    Task MarkAsReadAsync(int notificationId, string userId);

    /// <summary>
    /// Dismisses (hides) a single notification.
    /// </summary>
    Task DismissAsync(int notificationId, string userId);

    /// <summary>
    /// Marks all unread notifications as read for a user.
    /// </summary>
    Task MarkAllAsReadAsync(string userId);

    /// <summary>
    /// Auto-dismisses notifications matching the given reference when the issue resolves.
    /// </summary>
    Task AutoDismissAsync(int referenceId, string referenceType, NotificationType? specificType = null);
}
```

- [ ] **Step 1b: Create `GroupedNotification.cs`**

```csharp
namespace RentMate.Models.Domain;

/// <summary>
/// Groups duplicate notifications (same type + reference) for collapsed dropdown display.
/// </summary>
public class GroupedNotification
{
    public Notification LatestNotification { get; set; } = default!;
    public int Count { get; set; }
    public List<int> Ids { get; set; } = [];
}
```

- [ ] **Step 2: Create `NotificationService.cs`**

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RentMate.Hubs;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;

namespace RentMate.Services.Implementations;

/// <summary>
/// Persistent notification service with SignalR push and auto-dismiss.
/// </summary>
public class NotificationService(
    RentMateContext context,
    IHubContext<RentMateHub> hubContext) : INotificationService
{
    public async Task CreateAsync(string userId, NotificationType type, string title,
        string? message = null, int? referenceId = null,
        string? referenceType = null, string? actionUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            ActionUrl = actionUrl
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        // Push to connected client in real-time
        await hubContext.Clients.User(userId).SendAsync(RentMateHub.NewNotificationEvent, new
        {
            id = notification.Id,
            type = type.ToString(),
            title,
            message,
            actionUrl,
            createdAt = notification.CreatedAt.ToString("o")
        });
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDismissed);
    }

    public async Task<List<Notification>> GetRecentAsync(string userId, int limit = 20)
    {
        return await context.Notifications
            .Where(n => n.UserId == userId && !n.IsDismissed)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<GroupedNotification>> GetRecentGroupedAsync(string userId, int limit = 20)
    {
        var notifications = await context.Notifications
            .Where(n => n.UserId == userId && !n.IsDismissed)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100) // Fetch more for grouping
            .AsNoTracking()
            .ToListAsync();

        return notifications
            .GroupBy(n => new { n.ReferenceId, n.ReferenceType, n.Type })
            .Select(g => new GroupedNotification
            {
                LatestNotification = g.First(),
                Count = g.Count(),
                Ids = g.Select(n => n.Id).ToList()
            })
            .Take(limit)
            .ToList();
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task DismissAsync(int notificationId, string userId)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification != null && !notification.IsDismissed)
        {
            notification.IsDismissed = true;
            notification.IsRead = true;
            notification.ReadAt ??= DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsDismissed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    public async Task AutoDismissAsync(int referenceId, string referenceType, NotificationType? specificType = null)
    {
        var query = context.Notifications
            .Where(n => n.ReferenceId == referenceId
                && n.ReferenceType == referenceType
                && !n.IsDismissed);

        if (specificType.HasValue)
            query = query.Where(n => n.Type == specificType.Value);

        var notifications = await query.ToListAsync();
        foreach (var n in notifications)
        {
            n.IsDismissed = true;
            n.IsRead = true;
            n.ReadAt ??= DateTime.UtcNow;

            // Notify connected client to remove from dropdown
            await hubContext.Clients.User(n.UserId).SendAsync(
                RentMateHub.NotificationDismissedEvent,
                new { id = n.Id });
        }

        if (notifications.Count > 0)
            await context.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Register in `Program.cs` — add after line 309 (IAccountLifecycleService)**

```csharp
// --- Notifications ---
builder.Services.AddScoped<INotificationService, NotificationService>();
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build RentMate.sln`
Expected: Build succeeded

---

## Task 5: Notification Controller

**Files:**
- Create: `Controllers/Mvc/NotificationController.cs`

- [ ] **Step 1: Create the controller**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMate.Services.Interfaces;
using System.Security.Claims;

namespace RentMate.Controllers.Mvc;

/// <summary>
/// Endpoints for the notification bell UI.
/// </summary>
[Authorize]
public class NotificationController(INotificationService notificationService) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var count = await notificationService.GetUnreadCountAsync(UserId);
        return Json(new { count });
    }

    [HttpGet]
    public async Task<IActionResult> Recent()
    {
        var grouped = await notificationService.GetRecentGroupedAsync(UserId);
        var result = grouped.Select(g => new
        {
            g.LatestNotification.Id,
            type = g.LatestNotification.Type.ToString(),
            g.LatestNotification.Title,
            g.LatestNotification.Message,
            g.LatestNotification.ActionUrl,
            g.LatestNotification.IsRead,
            createdAt = g.LatestNotification.CreatedAt.ToString("o"),
            g.Count,
            ids = g.Ids
        });
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead([FromBody] NotificationIdRequest request)
    {
        await notificationService.MarkAsReadAsync(request.Id, UserId);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss([FromBody] NotificationIdRequest request)
    {
        await notificationService.DismissAsync(request.Id, UserId);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await notificationService.MarkAllAsReadAsync(UserId);
        return Json(new { success = true });
    }

    public record NotificationIdRequest(int Id);
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build RentMate.sln`
Expected: Build succeeded

---

## Task 6: Bell UI Partial

**Files:**
- Create: `Views/Shared/_NotificationBell.cshtml`

- [ ] **Step 1: Create the partial via Bash, then Read, then Write**

Run: `touch RentMate-Web/Views/Shared/_NotificationBell.cshtml`

Then write the full content:

```razor
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer

<div class="relative" id="notificationBellContainer">
    @* Bell Button *@
    <button onclick="window.NotificationBell.toggle()"
            class="relative p-2 text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-full transition-colors"
            aria-label="@Localizer["Notifications"]"
            aria-haspopup="true"
            id="notificationBellBtn">
        <i class="bi bi-bell text-lg"></i>
        <span id="notificationBadge"
              class="absolute -top-0.5 -right-0.5 min-w-5 h-5 px-1 bg-rose-500 text-white text-xs font-bold rounded-full flex items-center justify-center hidden">
            0
        </span>
    </button>

    @* Desktop Dropdown (hidden on mobile) *@
    <div id="notificationDropdown"
         class="hidden absolute right-0 mt-2 w-96 bg-white dark:bg-slate-800 rounded-2xl shadow-xl border border-slate-200 dark:border-slate-700 overflow-hidden md:block"
         style="z-index: var(--z-dropdown, 100);">

        @* Header *@
        <div class="px-4 py-3 border-b border-slate-100 dark:border-slate-700 flex items-center justify-between">
            <h3 class="text-sm font-bold text-slate-900 dark:text-white">@Localizer["Notifications"]</h3>
            <div class="flex items-center gap-2">
                <button onclick="window.NotificationBell.toggleSound()"
                        class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
                        id="notifSoundToggle" title="Toggle sound">
                    <i class="bi bi-volume-up text-sm"></i>
                </button>
                <button onclick="window.NotificationBell.markAllAsRead()"
                        class="text-xs text-blue-600 hover:text-blue-700 font-medium transition-colors"
                        id="markAllReadBtn">
                    @Localizer["Mark all as read"]
                </button>
            </div>
        </div>

        @* Notification List *@
        <div id="notificationList" class="max-h-96 overflow-y-auto divide-y divide-slate-100 dark:divide-slate-700">
            @* Rendered by notifications.js *@
        </div>

        @* Empty State (shown/hidden by JS) *@
        <div id="notificationEmpty" class="py-10 text-center hidden">
            <i class="bi bi-bell-slash text-3xl text-slate-300 dark:text-slate-600"></i>
            <p class="mt-2 text-sm text-slate-500 dark:text-slate-400">@Localizer["No notifications"]</p>
        </div>
    </div>
</div>

@* Mobile Slide-Up Panel (hidden on desktop) *@
<div id="notificationMobilePanel"
     class="fixed inset-0 hidden md:!hidden"
     style="z-index: var(--z-modal, 1200);">
    <div class="absolute inset-0 bg-black/40 backdrop-blur-sm" onclick="window.NotificationBell.closeMobile()"></div>
    <div class="absolute bottom-0 inset-x-0 bg-white dark:bg-slate-800 rounded-t-2xl shadow-2xl max-h-[80vh] flex flex-col transform transition-transform duration-300"
         id="notificationMobileSheet">
        @* Handle bar *@
        <div class="flex justify-center py-2">
            <div class="w-10 h-1 rounded-full bg-slate-300 dark:bg-slate-600"></div>
        </div>
        @* Header *@
        <div class="px-4 py-2 border-b border-slate-100 dark:border-slate-700 flex items-center justify-between">
            <h3 class="text-sm font-bold text-slate-900 dark:text-white">@Localizer["Notifications"]</h3>
            <div class="flex items-center gap-3">
                <button onclick="window.NotificationBell.markAllAsRead()"
                        class="text-xs text-blue-600 font-medium">@Localizer["Mark all as read"]</button>
                <button onclick="window.NotificationBell.closeMobile()"
                        class="text-slate-400 hover:text-slate-600"><i class="bi bi-x-lg"></i></button>
            </div>
        </div>
        @* Notification list for mobile *@
        <div id="notificationMobileList" class="flex-1 overflow-y-auto divide-y divide-slate-100 dark:divide-slate-700"></div>
        <div id="notificationMobileEmpty" class="py-10 text-center hidden">
            <i class="bi bi-bell-slash text-3xl text-slate-300 dark:text-slate-600"></i>
            <p class="mt-2 text-sm text-slate-500 dark:text-slate-400">@Localizer["No notifications"]</p>
        </div>
    </div>
</div>
```

- [ ] **Step 2: Insert bell partial in `_NavBar.cshtml`**

Insert `@await Html.PartialAsync("_NotificationBell")` at `_NavBar.cshtml` line 289, right before the user menu `<div class="relative">`, wrapped in a flex container:

```razor
@if (User.Identity?.IsAuthenticated == true)
{
    <div class="flex items-center gap-2">
        @await Html.PartialAsync("_NotificationBell")
        <!-- existing User Menu div follows -->
```

---

## Task 7: Client-Side JavaScript

**Files:**
- Create: `wwwroot/js/notifications.js`
- Modify: `Views/Shared/_Layout.cshtml` (add script tag)
- Modify: `wwwroot/js/layout.js` (add SignalR listeners for NewNotification/NotificationDismissed)

- [ ] **Step 1: Create `notifications.js`**

IIFE pattern (`(function() { 'use strict'; ... })();`). Expose `window.NotificationBell` object. Must handle:

**Core functions:**
- `fetchUnreadCount()`: GET `/Notification/UnreadCount` → update `#notificationBadge`
- `fetchAndRender(listId, emptyId)`: GET `/Notification/Recent` → render grouped items into target list container
- `renderNotification(n)`: creates DOM element with icon (per type category), title (with `(Nx)` suffix if `n.count > 1`), message, time ago, X button
- `dismissNotification(ids, el)`: POST `/Notification/Dismiss` for each ID in group with anti-forgery token → remove element → decrement badge
- `markAsRead(id)`: POST `/Notification/MarkAsRead`
- `markAllAsRead()`: POST `/Notification/MarkAllAsRead` → clear all badge counts
- `timeAgo(dateStr)`: relative time formatting using `window.T` keys
- Icon mapping: function that maps notification type prefix to Bootstrap Icon class + color
- Close dropdown on outside click
- Call `fetchUnreadCount()` on DOMContentLoaded (only if `data-authenticated="true"`)

**Mobile/desktop toggle:**
```javascript
toggle: function() {
    if (window.innerWidth < 768) {
        // Mobile: show slide-up panel
        var panel = document.getElementById('notificationMobilePanel');
        panel.classList.remove('hidden');
        this.fetchAndRender('notificationMobileList', 'notificationMobileEmpty');
    } else {
        // Desktop: toggle dropdown
        var dd = document.getElementById('notificationDropdown');
        var isHidden = dd.classList.contains('hidden');
        dd.classList.toggle('hidden');
        if (isHidden) this.fetchAndRender('notificationList', 'notificationEmpty');
    }
},
closeMobile: function() {
    document.getElementById('notificationMobilePanel').classList.add('hidden');
}
```

**Notification sound:**
```javascript
// At top of IIFE
var notifSound = null;
var soundEnabled = localStorage.getItem('notifSound') !== 'false';

function playNotifSound() {
    if (!soundEnabled) return;
    if (!notifSound) {
        notifSound = new Audio('/sounds/notification.mp3');
        notifSound.volume = 0.3;
    }
    notifSound.play().catch(function() {}); // Ignore autoplay restrictions
}
```

**Sound toggle** (updates speaker icon in dropdown header):
```javascript
toggleSound: function() {
    soundEnabled = !soundEnabled;
    localStorage.setItem('notifSound', soundEnabled ? 'true' : 'false');
    var icon = document.querySelector('#notifSoundToggle i');
    if (icon) icon.className = soundEnabled ? 'bi bi-volume-up text-sm' : 'bi bi-volume-mute text-sm';
}
```

**Grouping display:** When rendering, if `n.count > 1`, append `(${n.count}x)` after the title text. When dismissing, send dismiss requests for all IDs in `n.ids` array.

- [ ] **Step 2: Add SignalR listeners in `layout.js`**

After the existing `DepositStatusChanged` listener (around line 259), add:

```javascript
conn.on('NewNotification', function(data) {
    // Bump notification bell badge
    var badge = document.getElementById('notificationBadge');
    if (badge) {
        var count = parseInt(badge.textContent, 10) || 0;
        badge.textContent = ++count;
        badge.classList.remove('hidden');
    }

    // Play sound if dropdown not open
    var dropdown = document.getElementById('notificationDropdown');
    if (!dropdown || dropdown.classList.contains('hidden')) {
        if (typeof playNotifSound === 'function') playNotifSound();
    }
});

conn.on('NotificationDismissed', function(data) {
    // Decrement notification bell badge
    var badge = document.getElementById('notificationBadge');
    if (badge) {
        var count = parseInt(badge.textContent, 10) || 0;
        if (count > 1) {
            badge.textContent = --count;
        } else {
            badge.textContent = '0';
            badge.classList.add('hidden');
        }
    }
});
```

- [ ] **Step 3: Add script tag in `_Layout.cshtml`**

Insert after line 120 (`signalr.min.js`):

```html
<script src="~/js/notifications.js" asp-append-version="true"></script>
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build RentMate.sln`
Expected: Build succeeded

---

## Task 8: Localization Keys

**Files:**
- Modify: `Resources/en.json`
- Modify: `Resources/sl.json`

- [ ] **Step 1: Add all notification-related keys to both files**

Keys needed (alphabetically sorted, insert at correct positions):

```
"Mark all as read": "Mark all as read" / "Označi vse kot prebrano"
"New counter-offer received": "New counter-offer received" / "Prejeta nova nasprotna ponudba"
"New rental request": "New rental request" / "Nova prošnja za najem"
"No notifications": "No notifications" / "Ni obvestil"
"Notification.DepositCharged": "Deposit charged" / "Varščina zaračunana"
"Notification.DepositCounterOffered": "Counter-offer received" / "Nasprotna ponudba prejeta"
"Notification.DepositDisputed": "Deposit disputed" / "Varščina izpodbijana"
"Notification.DepositEscalated": "Dispute escalated" / "Spor posredovan"
"Notification.DepositReleased": "Deposit released" / "Varščina sproščena"
"Notification.DepositResolved": "Dispute resolved" / "Spor razrešen"
"Notification.ExtensionApproved": "Extension approved" / "Podaljšanje odobreno"
"Notification.ExtensionAutoApproved": "Extension auto-approved" / "Podaljšanje samodejno odobreno"
"Notification.ExtensionCancelled": "Extension cancelled" / "Podaljšanje preklicano"
"Notification.ExtensionDeclined": "Extension declined" / "Podaljšanje zavrnjeno"
"Notification.ExtensionPaid": "Extension paid" / "Podaljšanje plačano"
"Notification.ExtensionRequested": "Extension requested" / "Podaljšanje zaprošeno"
"Notification.PaymentFailed": "Payment failed" / "Plačilo neuspešno"
"Notification.PaymentSucceeded": "Payment succeeded" / "Plačilo uspešno"
"Notification.RentalAccepted": "Rental accepted" / "Najem sprejet"
"Notification.RentalCancelled": "Rental cancelled" / "Najem preklican"
"Notification.RentalCompleted": "Rental completed" / "Najem zaključen"
"Notification.RentalOverdue": "Rental overdue" / "Najem v zamudi"
"Notification.RentalRequested": "New rental request" / "Nova prošnja za najem"
"Notification.ReviewReceived": "New review received" / "Novo mnenje prejeto"
"Notifications": "Notifications" / "Obvestila"
"just now": "just now" / "pravkar"
"minutes ago": "minutes ago" / "minut nazaj"
"hours ago": "hours ago" / "ur nazaj"
"days ago": "days ago" / "dni nazaj"
```

**Additional format string keys for localized notification messages:**

```
"NotificationMsg.RentalRequested": "{0} wants to rent {1}" / "{0} želi najeti {1}"
"NotificationMsg.RentalAccepted": "{0} — awaiting payment" / "{0} — čaka na plačilo"
"NotificationMsg.RentalCancelled": "{0} was cancelled" / "{0} je bil preklican"
"NotificationMsg.RentalCompleted": "{0} has been completed" / "{0} je zaključen"
"NotificationMsg.RentalOverdue": "{0} is {1} days overdue" / "{0} je {1} dni v zamudi"
"NotificationMsg.ExtensionRequested": "{0}: extend to {1}" / "{0}: podaljšaj do {1}"
"NotificationMsg.ExtensionApproved": "{0} — extension approved" / "{0} — podaljšanje odobreno"
"NotificationMsg.ExtensionDeclined": "{0} — extension declined" / "{0} — podaljšanje zavrnjeno"
"NotificationMsg.ExtensionCancelled": "{0} — extension cancelled" / "{0} — podaljšanje preklicano"
"NotificationMsg.ExtensionPaid": "{0} — extension paid" / "{0} — podaljšanje plačano"
"NotificationMsg.DepositCharged": "{0} — {1} charged" / "{0} — zaračunano {1}"
"NotificationMsg.DepositReleased": "{0} — deposit released" / "{0} — varščina sproščena"
"NotificationMsg.DepositDisputed": "{0} — deposit disputed" / "{0} — varščina izpodbijana"
"NotificationMsg.DepositCounterOffered": "{0} — counter-offer received" / "{0} — prejeta nasprotna ponudba"
"NotificationMsg.DepositEscalated": "{0} — dispute escalated" / "{0} — spor posredovan"
"NotificationMsg.DepositResolved": "{0} — dispute resolved" / "{0} — spor razrešen"
"NotificationMsg.DeadlineAutoResolved": "Auto-resolved for {0}" / "Samodejno razrešeno za {0}"
"NotificationMsg.ReviewReceived": "{0} left a {1}★ review on {2}" / "{0} je podal/a {1}★ mnenje o {2}"
"NotificationMsg.PaymentSucceeded": "{0} — payment succeeded" / "{0} — plačilo uspešno"
"NotificationMsg.PaymentFailed": "{0} — payment failed" / "{0} — plačilo neuspešno"
```

Use `node -e` script to merge keys into both JSON files, maintaining alphabetical sort.

**Note on localization in integration tasks (Tasks 9-13):** All `CreateAsync` calls must use `_localizer["Notification.X"].Value` for titles and `string.Format(_localizer["NotificationMsg.X"].Value, ...)` for messages instead of hardcoded English strings. Each MVC controller already has `IViewLocalizer` or `IStringLocalizer` injected. For `OverdueRentalService` (background service), resolve `IStringLocalizerFactory` from the scoped service provider.

---

## Task 9: Integration — RentalsController

**Files:**
- Modify: `Controllers/Mvc/RentalsController.cs`

- [ ] **Step 1: Add `INotificationService` to constructor DI**

Add field: `private readonly INotificationService _notificationService;`
Add parameter to constructor and assign.

- [ ] **Step 2: Add CreateAsync call in `NotifyOwnerOfRentalRequestAsync` (after line 373)**

```csharp
await _notificationService.CreateAsync(
    item.UserId!,
    NotificationType.RentalRequested,
    _localizer["Notification.RentalRequested"].Value,
    string.Format(_localizer["NotificationMsg.RentalRequested"].Value,
        renter.FirstName ?? renter.UserName, item.Title),
    rental.Id, "Rental", "/Dashboard?tab=lending");
```

- [ ] **Step 3: Add CreateAsync + AutoDismiss in `NotifyRentalStatusChangeAsync` (after line 112)**

```csharp
var type = rental.Status switch
{
    RentalStatus.Accepted => NotificationType.RentalAccepted,
    RentalStatus.Completed => NotificationType.RentalCompleted,
    RentalStatus.Cancelled => NotificationType.RentalCancelled,
    _ => NotificationType.RentalApproved
};
var titleKey = "Notification." + type.ToString();
var msgKey = "NotificationMsg." + type.ToString();
await _notificationService.CreateAsync(
    rental.RenterId!, type,
    _localizer[titleKey].Value,
    string.Format(_localizer[msgKey].Value, rental.Item?.Title ?? ""),
    rental.Id, "Rental", "/Dashboard?tab=renting");

// Auto-dismiss the original request notification for the owner
if (rental.Status != RentalStatus.Pending)
    await _notificationService.AutoDismissAsync(rental.Id, "Rental", NotificationType.RentalRequested);
```

- [ ] **Step 4: Build to verify**

---

## Task 10: Integration — DashboardController

**Files:**
- Modify: `Controllers/Mvc/DashboardController.cs`

- [ ] **Step 1: Add `INotificationService` to constructor DI**

- [ ] **Step 2: Add notifications in extension actions**

**RequestExtension** (after the existing SendAsync around line 162):
```csharp
await _notificationService.CreateAsync(
    rental.OwnerId!, NotificationType.ExtensionRequested,
    _localizer["Notification.ExtensionRequested"].Value,
    string.Format(_localizer["NotificationMsg.ExtensionRequested"].Value,
        rental.Item?.Title ?? "", ext.NewEndDate.ToString("dd MMM")),
    ext.Id, "Extension", "/Dashboard?tab=lending");
```

**ApproveExtension** (after SendAsync around line 203):
```csharp
await _notificationService.CreateAsync(
    ext.RequestedByUserId!, NotificationType.ExtensionApproved,
    _localizer["Notification.ExtensionApproved"].Value,
    string.Format(_localizer["NotificationMsg.ExtensionApproved"].Value, rental.Item?.Title ?? ""),
    ext.Id, "Extension", "/Dashboard?tab=renting");
await _notificationService.AutoDismissAsync(ext.Id, "Extension", NotificationType.ExtensionRequested);
```

**DeclineExtension** (after SendAsync around line 236):
```csharp
await _notificationService.CreateAsync(
    decExt.RequestedByUserId!, NotificationType.ExtensionDeclined,
    _localizer["Notification.ExtensionDeclined"].Value,
    string.Format(_localizer["NotificationMsg.ExtensionDeclined"].Value, rental.Item?.Title ?? ""),
    decExt.Id, "Extension", "/Dashboard?tab=renting");
await _notificationService.AutoDismissAsync(decExt.Id, "Extension", NotificationType.ExtensionRequested);
```

**CancelExtension** (after SendAsync around line 268):
```csharp
await _notificationService.CreateAsync(
    cancelExt.Rental!.OwnerId!, NotificationType.ExtensionCancelled,
    _localizer["Notification.ExtensionCancelled"].Value,
    string.Format(_localizer["NotificationMsg.ExtensionCancelled"].Value, rental.Item?.Title ?? ""),
    cancelExt.Id, "Extension", "/Dashboard?tab=lending");
await _notificationService.AutoDismissAsync(cancelExt.Id, "Extension", NotificationType.ExtensionRequested);
```

- [ ] **Step 3: Build to verify**

---

## Task 11: Integration — DisputeController

**Files:**
- Modify: `Controllers/Mvc/DisputeController.cs`

This controller has 10+ SendAsync calls. For each, add a `_notificationService.CreateAsync(...)` call right after the existing `SendAsync`. All use `referenceType: "Deposit"` and `referenceId: rental.Id` (since deposit is 1:1 with rental).

- [ ] **Step 1: Add `INotificationService` to constructor DI**

- [ ] **Step 2: Add CreateAsync calls after each SendAsync**

| Action | Line | Type | Recipient | Auto-dismiss |
|--------|------|------|-----------|-------------|
| ReleaseDeposit | ~89 | DepositReleased | RenterId | AutoDismiss(rentalId, "Deposit") |
| ChargeDeposit | ~129 | DepositCharged | RenterId | — |
| ReleaseDisputedDeposit | ~161 | DepositReleased | RenterId | AutoDismiss(rentalId, "Deposit") |
| CompleteWithDeposit | ~227 | DepositReleased or DepositCharged | RenterId | AutoDismiss if released |
| DisputeDeposit | ~263 | DepositDisputed | OwnerId | AutoDismiss(rentalId, "Deposit", DepositCharged) |
| AcceptCharge | ~295 | DepositResolved | OwnerId | AutoDismiss(rentalId, "Deposit") |
| AcceptCounterOffer | ~327 | DepositResolved | OwnerId | AutoDismiss(rentalId, "Deposit") |
| RejectCounterOffer | ~359 | DepositEscalated (if auto-escalated) or DepositDisputed | OwnerId | — |
| CounterOfferDeposit | ~397 | DepositCounterOffered | RenterId | AutoDismiss(rentalId, "Deposit", DepositDisputed) |
| EscalateDispute | ~440 | DepositEscalated | recipientId (other party) | — |
| AdminResolveDispute | ~527,534 | DepositResolved | Both parties | AutoDismiss(rentalId, "Deposit") |

Follow the pattern (using localized strings):
```csharp
await _notificationService.CreateAsync(
    recipientUserId, NotificationType.XYZ,
    _localizer["Notification.XYZ"].Value,
    string.Format(_localizer["NotificationMsg.XYZ"].Value, itemTitle),
    rentalId, "Deposit", "/Dashboard");
```

- [ ] **Step 3: Build to verify**

---

## Task 12: Integration — PaymentController & ReviewsController

**Files:**
- Modify: `Controllers/Mvc/PaymentController.cs`
- Modify: `Controllers/Mvc/ReviewsController.cs`

- [ ] **Step 1: PaymentController — Add DI + CreateAsync in `ExtensionSuccess` (after SendAsync ~line 303)**

```csharp
await _notificationService.CreateAsync(
    extension.Rental!.OwnerId!, NotificationType.ExtensionPaid,
    _localizer["Notification.ExtensionPaid"].Value,
    string.Format(_localizer["NotificationMsg.ExtensionPaid"].Value, extension.Rental.Item?.Title ?? ""),
    extension.Id, "Extension", "/Dashboard?tab=lending");
await _notificationService.AutoDismissAsync(extension.Id, "Extension", NotificationType.ExtensionApproved);
```

- [ ] **Step 2: ReviewsController — Add `INotificationService` DI + CreateAsync after review creation**

Add `INotificationService` to `ReviewsController` constructor DI. In the `Create` action, after `SaveChangesAsync` (line 149) and `UpdateItemAggregatesAsync` (line 150):

```csharp
// After line 150 (UpdateItemAggregatesAsync)
var item = await _context.Items.FindAsync(request.ItemId);
if (item != null)
{
    var reviewer = await _context.Users.FindAsync(userId);
    await _notificationService.CreateAsync(
        item.UserId!, NotificationType.ReviewReceived,
        _localizer["Notification.ReviewReceived"].Value,
        string.Format(_localizer["NotificationMsg.ReviewReceived"].Value,
            reviewer?.FirstName ?? reviewer?.UserName ?? "",
            request.Rating, item.Title),
        review.Id, "Review",
        $"/Items/Details/{item.Id}#reviews");
}
```

**Important:** Use MVC `Controllers/Mvc/ReviewsController.cs`, NOT `Controllers/Api/ReviewApiController.cs` (API controllers are read-only per CLAUDE.md).

- [ ] **Step 3: Build to verify**

---

## Task 13: Integration — OverdueRentalService (Background)

**Files:**
- Modify: `Services/Implementations/OverdueRentalService.cs`

- [ ] **Step 1: Add `INotificationService` and `IStringLocalizerFactory` to constructor and resolve from scope**

Since this is a `BackgroundService`, resolve both `INotificationService` and `IStringLocalizerFactory` from the scoped service provider. Create localizer instance: `var localizer = localizerFactory.Create("SharedResource", "RentMate");`

- [ ] **Step 2: Add CreateAsync calls in `CheckOverdueRentalsAsync`**

After lines 80 and 87 (where RentalOverdueEvent is sent to renter and owner):
```csharp
await notificationService.CreateAsync(
    rental.RenterId!, NotificationType.RentalOverdue,
    localizer["Notification.RentalOverdue"].Value,
    string.Format(localizer["NotificationMsg.RentalOverdue"].Value,
        rental.Item?.Title ?? "", daysOverdue),
    rental.Id, "Rental", "/Dashboard?tab=renting");

await notificationService.CreateAsync(
    rental.OwnerId!, NotificationType.RentalOverdue,
    localizer["Notification.RentalOverdue"].Value,
    string.Format(localizer["NotificationMsg.RentalOverdue"].Value,
        rental.Item?.Title ?? "", daysOverdue),
    rental.Id, "Rental", "/Dashboard?tab=lending");
```

**Important:** Add deduplication — only create if no unread RentalOverdue notification exists for this rental in the last 24 hours (to avoid hourly spam). Check before creating:
```csharp
var recentExists = await context.Notifications.AnyAsync(n =>
    n.UserId == rental.RenterId && n.ReferenceId == rental.Id
    && n.ReferenceType == "Rental" && n.Type == NotificationType.RentalOverdue
    && !n.IsDismissed && n.CreatedAt > DateTime.UtcNow.AddHours(-24));
if (!recentExists) { /* create */ }
```

- [ ] **Step 3: Add CreateAsync in `CheckDisputeDeadlinesAsync`**

After each deadline auto-resolution SendAsync (~lines 124, 138, 153):
```csharp
await notificationService.CreateAsync(
    notifyUserId, NotificationType.DeadlineAutoResolved,
    localizer["Notification.DepositResolved"].Value,
    string.Format(localizer["NotificationMsg.DeadlineAutoResolved"].Value, rental.Item?.Title ?? ""),
    rental.Id, "Deposit", "/Dashboard");
```

- [ ] **Step 4: Build to verify**

---

## Task 14: Data Retention Cleanup

**Files:**
- Modify: `Services/Implementations/DataRetentionService.cs`

- [ ] **Step 1: Add `PurgeOldNotificationsAsync` method**

Add after the last purge method, following the existing pattern:

```csharp
/// <summary>
/// Deletes dismissed notifications older than 30 days and all notifications older than 90 days.
/// </summary>
private async Task PurgeOldNotificationsAsync(RentMateContext context, CancellationToken ct)
{
    var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
    var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);

    var deletedDismissed = await context.Notifications
        .Where(n => n.IsDismissed && n.CreatedAt < thirtyDaysAgo)
        .ExecuteDeleteAsync(ct);

    var deletedOld = await context.Notifications
        .Where(n => n.CreatedAt < ninetyDaysAgo)
        .ExecuteDeleteAsync(ct);

    if (deletedDismissed + deletedOld > 0)
        _logger.LogInformation("Purged {Dismissed} dismissed + {Old} old notifications.",
            deletedDismissed, deletedOld);
}
```

- [ ] **Step 2: Call it from `RunRetentionPassAsync` (after line 72)**

```csharp
await PurgeOldNotificationsAsync(context, ct);
```

- [ ] **Step 3: Build to verify**

---

## Task 15: Final Build & Manual Verification

- [ ] **Step 1: Full build**

Run: `dotnet build RentMate.sln`
Expected: 0 errors

- [ ] **Step 2: Run the app**

Run: `dotnet run --project RentMate-Web/RentMate.csproj`

Verify:
1. Bell icon visible in navbar for authenticated users, hidden for anonymous
2. Bell shows "0" badge (hidden) on fresh account
3. Clicking bell opens dropdown with "No notifications" empty state
4. Creating a rental → owner sees notification appear in bell
5. Approving a rental → "pending request" notification auto-dismisses for owner
6. Clicking X on a notification → it disappears, count decrements
7. Clicking notification body → navigates to correct page
8. "Mark all as read" → badge goes to 0
9. Page refresh → notifications persist (loaded from DB)
10. SignalR push → badge bumps in real-time without page refresh
11. Grouped notifications: trigger 3 overdue events for same rental → dropdown shows "Rental overdue (3x)" as single entry
12. Sound: new notification plays chime when dropdown is closed; no sound when dropdown is open; mute toggle works
13. Mobile: on narrow viewport (<768px), bell opens bottom slide-up sheet instead of dropdown
14. Localization: switch to Slovenian → all notification titles/messages appear in Slovenian
15. Review notification: leave a review → item owner sees "New review received" in bell

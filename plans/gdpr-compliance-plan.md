# GDPR Compliance & Data Deletion Overhaul

## Context

The current data deletion system has critical bugs and GDPR compliance gaps:
- **Admin deletion** (`UsersController.DeleteConfirmed`) crashes with FK violations — no cleanup logic
- **API deletion** (`UsersApiController.DeleteUser`) same — has a TODO acknowledging it
- **Two-tier user deletion** (anonymize vs full-delete) is confusing and inconsistent
- **Data export** only includes `ApplicationUser` fields, missing rentals/reviews/items/payments/disputes (GDPR Art. 20)
- **No data retention policies** — data accumulates forever
- **No consent management** — no cookie consent, no registration consent tracking
- **No privacy policy page**

### Intended Outcome
A GDPR-compliant system with:
1. Clear **Deactivate** (reversible) + **Delete** (irreversible anonymization) account model
2. All deletion paths working correctly (no FK crashes)
3. Complete personal data export
4. 5-year data retention with automated cleanup
5. Cookie consent banner + registration consent
6. Privacy policy page in both languages

---

## Phase 1: Database Schema & Models

### Step 1: New Fields & Entities

**`ApplicationUser` — new fields:**
```csharp
// Deactivation
public bool IsDeactivated { get; set; }
public DateTime? DeactivatedAt { get; set; }
public DeactivationSource? DeactivatedBy { get; set; }  // User or Admin
public string? DeactivationReason { get; set; }          // Admin-provided reason

// GDPR Consent
public DateTime? PrivacyPolicyAcceptedAt { get; set; }
public string? PrivacyPolicyVersion { get; set; }        // e.g. "1.0"
```

**New enum `DeactivationSource`** in `Models/Domain/`:
```csharp
public enum DeactivationSource { User, Admin }
```

**New entity `CookieConsent`** in `Models/Domain/`:
```csharp
public class CookieConsent
{
    public int Id { get; set; }
    public string? UserId { get; set; }           // Nullable for anonymous
    public bool NecessaryCookies { get; set; }     // Always true
    public bool AnalyticsCookies { get; set; }
    public bool MarketingCookies { get; set; }
    public string? IpAddressHash { get; set; }     // SHA256 hashed
    public DateTime ConsentedAt { get; set; }
    public string? UserAgent { get; set; }

    public virtual ApplicationUser? User { get; set; }
}
```

**`RentMateContext`:**
- Add `DbSet<CookieConsent> CookieConsents`
- Configure CookieConsent → ApplicationUser (Cascade on delete)

**Migration:** `AddGdprFields`

**Files to modify:**
- `Models/Domain/ApplicationUser.cs` — add 6 new properties
- `Models/Domain/DeactivationSource.cs` — new file
- `Models/Domain/CookieConsent.cs` — new file
- `Infrastructure/Data/RentMateContext.cs` — add DbSet + configuration
- EF migration

---

## Phase 2: Account Deactivation System

### Step 2: Deactivation Service

Create `IAccountLifecycleService` / `AccountLifecycleService` to centralize all account operations (deactivate, reactivate, delete). This replaces the scattered logic in `Security.cshtml.cs` and `UsersController`.

**`Services/Interfaces/IAccountLifecycleService.cs`:**
```csharp
public interface IAccountLifecycleService
{
    Task<bool> HasActiveRentalsAsync(string userId);
    Task DeactivateAccountAsync(string userId, DeactivationSource source, string? reason = null);
    Task ReactivateAccountAsync(string userId);
    Task DeleteAccountAsync(string userId);  // Irreversible anonymization
}
```

**`Services/Implementations/AccountLifecycleService.cs`:**

Dependencies: `RentMateContext`, `UserManager<ApplicationUser>`, `IFileUploadService`, `IPaymentService`, `ILogger`

**DeactivateAccountAsync:**
1. Set `IsDeactivated = true`, `DeactivatedAt = now`, `DeactivatedBy = source`
2. If admin: set `DeactivationReason`
3. Delist all user's items: `item.IsListed = false` for all items (reuse existing `IsListed` field)
4. Sign out user (invalidate security stamp)
5. Log the action

**ReactivateAccountAsync:**
1. Set `IsDeactivated = false`, clear `DeactivatedAt/By/Reason`
2. Re-list items: `item.IsListed = true` (except `IsAdminHidden` items)
3. Log the action

**DeleteAccountAsync (replaces both old anonymize + full-delete):**
1. Validate no active rentals (throw if any)
2. Begin transaction
3. Clean up Cloudinary images (profile picture + all item images + dispute evidence)
4. Clean up Stripe customer
5. Hard-delete all user's items (cascade handles ItemImages, ItemAccessories, Favorites on those items; Rentals of those items get cascade deleted too — BUT only if the user is the owner)
6. For rentals where user is RENTER: preserve. Renter reference stays pointing to anonymized user.
7. Nullify `Payment.UserId` where `UserId == userId`
8. Anonymize user fields: FirstName → "Deleted User", LastName/City/Phone/Bio/ProfilePictureUrl/CategoryAffinityJson/Lat/Lng → null, verification flags → false
9. Anonymize email → `deleted_{Guid.NewGuid():N[..12]}@deleted.rentmate` (use GUID, not user ID, to avoid ID leakage)
10. Lock account: lockout enabled + DateTimeOffset.MaxValue
11. Remove password, update security stamp
12. Delete favorites
13. Commit transaction
14. Sign out

**Key change from current system:** Reviews written by the deleted user are PRESERVED (not hard-deleted). The reviewer shows as "Deleted User" via the anonymized FirstName. This avoids the FK Restrict violation risk and preserves item ratings.

**Files to create:**
- `Services/Interfaces/IAccountLifecycleService.cs`
- `Services/Implementations/AccountLifecycleService.cs`

**Files to modify:**
- `Program.cs` — register `IAccountLifecycleService` as Scoped

### Step 3: Deactivation Middleware

Create an action filter `DeactivatedAccountFilter` that runs on every authenticated request:
- If `user.IsDeactivated == true`:
  - Allow access to: deactivation page, reactivation endpoint, logout, cookie consent API
  - Redirect all other requests to `/Account/Deactivated`

**Files to create:**
- `Infrastructure/Filters/DeactivatedAccountFilter.cs`

**Files to modify:**
- `Program.cs` — register as global MVC filter

### Step 4: Deactivation UI (User-Initiated)

**Modify `Security.cshtml.cs`:**
- Replace current two-option delete system with:
  - "Deactivate Account" button → calls `IAccountLifecycleService.DeactivateAccountAsync(userId, DeactivationSource.User)`
  - "Delete Account" button → calls `IAccountLifecycleService.DeleteAccountAsync(userId)`
- Both require password confirmation (keep existing validation)
- Both blocked if active rentals exist (keep existing check)
- Remove `DeleteInput.DeleteAllData` toggle — no longer needed
- Update the view (`Security.cshtml`) to show two clear buttons with explanations:
  - Deactivate: "Your account will be hidden and your listings delisted. You can reactivate at any time by logging back in."
  - Delete: "Your personal data will be permanently deleted. Anonymized transaction records are retained as required by law. This action cannot be undone."

**Files to modify:**
- `Areas/Identity/Pages/Account/Manage/Security.cshtml.cs` — replace delete/anonymize methods with service calls
- `Areas/Identity/Pages/Account/Manage/Security.cshtml` — update delete section UI

### Step 5: Deactivated Account Page

Create a page users see when logged in while deactivated.

**New controller action** in `AccountController` (or a new page):
- `GET /Account/Deactivated` — shows status:
  - **User-deactivated**: "Your account is deactivated." + "Reactivate Account" button
  - **Admin-deactivated**: "Your account has been deactivated by an administrator. Reason: ___. To request reactivation, submit a request." + link to reactivation request (via dispute system — separate design doc)
- `POST /Account/Reactivate` — only works if `DeactivatedBy == User`. Calls `IAccountLifecycleService.ReactivateAccountAsync()`

**Files to modify:**
- `Controllers/Mvc/AccountController.cs` — add Deactivated + Reactivate actions
- New view: `Views/Account/Deactivated.cshtml`
- Localization keys in `en.json` and `sl.json`

### Step 6: Admin Deactivation & Deletion

**Modify `UsersController`:**
- Replace `DeleteConfirmed` (line 169-186) with a call to `IAccountLifecycleService.DeleteAccountAsync()`
- Add active rental check before deletion
- Add "Deactivate" button to admin user management (calls `IAccountLifecycleService.DeactivateAccountAsync(userId, DeactivationSource.Admin, reason)`)
- Admin provides a reason when deactivating
- Add "Reactivate" button for admin-deactivated users (calls `ReactivateAccountAsync`)
- Keep existing Ban/Unban as separate from Deactivate (ban = lockout, deactivate = soft hide)

**Files to modify:**
- `Controllers/Mvc/UsersController.cs` — fix DeleteConfirmed, add Deactivate/Reactivate actions
- `Views/Users/Index.cshtml` — add Deactivate button
- `Views/Users/Delete.cshtml` — update confirmation text
- Localization keys

---

## Phase 3: Deletion Bug Fixes

### Step 7: API Controller Issues (KNOWN LIMITATION)

**Note:** `Controllers/Api/` is read-only per project rules (serves mobile app). The following bugs exist but cannot be fixed in this plan:
- `UsersApiController.DeleteUser` (line 140-155) — no cleanup, will FK-crash
- `ItemsApiController.DeleteItem` — no Cloudinary cleanup

**Recommendation:** Coordinate with mobile team to either:
- Disable these endpoints and handle via web UI only
- Or create a follow-up task to fix API controllers with mobile team coordination

### Step 8: Review Deletion Consistency

Standardize on soft-delete only for reviews everywhere:
- Account deletion (new `DeleteAccountAsync`) already preserves reviews (step 2)
- Verify `ReviewsController.Delete()` uses soft-delete (it does — `IsDeleted = true`)
- No changes needed here, just validation

### Step 9: Null Handling in Views

Audit and fix views that reference user data to handle anonymized/deleted users gracefully:
- Replace "Unknown" fallbacks with localized "Deleted User" / "Izbrisani uporabnik"
- Ensure all user references use null-coalescing: `user?.FirstName ?? Localizer["DeletedUser"]`

**Files to check:**
- `Views/Items/Details.cshtml` (lines 147, 185)
- `Views/Payment/Success.cshtml` (line 29)
- `Views/Dashboard/_AttentionBanner.cshtml` (line 71)
- `Views/Dispute/AdminReviewDispute.cshtml` (lines 222-232)
- `Views/Dispute/AdminResolvedDisputes.cshtml`
- Any other views referencing `User`, `Owner`, `Renter`, `Reviewer`

---

## Phase 4: Data Export (GDPR Art. 20)

### Step 10: Complete Data Export

Expand `CollectPersonalDataAsync()` in `Security.cshtml.cs` to include all user-related data:

```csharp
var exportData = new
{
    Profile = personalDataFields,           // existing
    ExternalLogins = loginData,             // existing
    Items = userItems,                      // NEW: all items with details
    RentalsAsRenter = renterRentals,        // NEW: rental history as renter
    RentalsAsOwner = ownerRentals,          // NEW: rental history as owner
    Reviews = userReviews,                  // NEW: reviews written
    Payments = userPayments,               // NEW: payment records
    Favorites = userFavorites,              // NEW: favorited items
    Deposits = rentalDeposits,             // NEW: deposit/dispute records
    Extensions = rentalExtensions,          // NEW: extension requests
    CookieConsent = cookieConsentRecords    // NEW: consent history
};
```

Export as JSON file download (reuse existing download mechanism).

**Files to modify:**
- `Areas/Identity/Pages/Account/Manage/Security.cshtml.cs` — expand `CollectPersonalDataAsync()`

---

## Phase 5: Data Retention

### Step 11: Retention Background Service

Create `DataRetentionService` (IHostedService, runs daily at 3 AM):

**Retention rules (5-year default):**
1. Completed/cancelled rentals older than 5 years → hard-delete (cascades to deposits, extensions, accessories, payments)
2. Anonymized user records (`deleted_*@deleted.rentmate`) with no rentals left → hard-delete the user record entirely
3. Soft-deleted reviews older than 1 year → hard-delete
4. Orphaned Cloudinary images (dispute evidence on deleted rentals) → cleaned up

**Audit logging:** All retention deletions logged with entity type, count, and date range.

**Files to create:**
- `Services/Implementations/DataRetentionService.cs`

**Files to modify:**
- `Program.cs` — register as `AddHostedService<DataRetentionService>`

---

## Phase 6: Consent & Privacy

### Step 12: Cookie Consent Banner

**Frontend (vanilla JS, consistent with existing stack):**
- Cookie consent banner component in `wwwroot/js/cookie-consent.js`
- Three categories: Necessary (always on, disabled toggle), Analytics, Marketing
- Banner appears on first visit or when consent not stored
- Preferences stored in `localStorage` + `rentmate_consent` cookie
- "Manage Preferences" link in footer opens settings modal
- Styling: Tailwind classes, dark mode compatible, consistent with site design

**Backend:**
- `POST /api/cookie-consent` endpoint (in a new `CookieConsentController` or existing controller) to persist consent to DB
- Store: categories accepted, timestamp, hashed IP, user agent
- Link to authenticated user if logged in

**Files to create:**
- `wwwroot/js/cookie-consent.js`
- Partial view: `Views/Shared/_CookieConsent.cshtml`

**Files to modify:**
- `Views/Shared/_Layout.cshtml` — include cookie consent partial + script
- `Controllers/Mvc/` — add cookie consent endpoint (or new controller)
- Localization keys in `en.json` and `sl.json`

### Step 13: Registration Consent

**Modify registration page:**
- Add required checkbox: "I agree to the Privacy Policy and Terms of Service and consent to data processing as described"
- Link to privacy policy page
- On registration: set `PrivacyPolicyAcceptedAt = DateTime.UtcNow`, `PrivacyPolicyVersion = "1.0"`

**Policy version re-consent:**
- On login: if `PrivacyPolicyVersion != CurrentVersion` → redirect to re-consent page
- User must accept updated policy before proceeding

**Files to modify:**
- `Areas/Identity/Pages/Account/Register.cshtml` — add consent checkbox
- `Areas/Identity/Pages/Account/Register.cshtml.cs` — validate + save consent fields
- Localization keys

### Step 14: Privacy Policy Page

**New `PrivacyController`** with `Index` action:
- Renders `Views/Privacy/Index.cshtml`
- Content via localization keys (both sl and en)

**Privacy policy template content covering:**
1. Data controller information (RentMate platform)
2. What data is collected (registration data, rental data, payment data, usage data, cookies)
3. Legal basis for processing (consent, contract performance, legitimate interest, legal obligation)
4. How data is used (providing service, communication, safety/trust, analytics)
5. Data sharing (payment processors — Stripe, image hosting — Cloudinary)
6. Data retention periods (5 years for transaction data, 1 year for deleted reviews)
7. User rights (access, rectification, erasure, portability, restriction, objection)
8. How to exercise rights (account settings, contact info)
9. Cookie policy (categories, what each does)
10. Changes to policy (versioned, re-consent required)
11. Contact information

**Files to create:**
- `Controllers/Mvc/PrivacyController.cs`
- `Views/Privacy/Index.cshtml`

**Files to modify:**
- `Views/Shared/_Layout.cshtml` — add Privacy Policy link to footer
- `Resources/en.json` — privacy policy content keys
- `Resources/sl.json` — privacy policy content keys (Slovenian translation)

---

## Phase 7: Cleanup & Finalization

### Step 15: Remove Old Deletion Code

After the new system is working:
- Remove `AnonymizeUserAccountAsync()` from `Security.cshtml.cs`
- Remove `DeleteAllUserDataAsync()` + `CleanupAllUserReferencesAsync()` from `Security.cshtml.cs`
- Remove `CleanupCloudinaryImagesAsync()` — move to service if not already
- Remove `DeleteInput.DeleteAllData` model property
- Clean up any dead code

### Step 16: Migration & Testing Checklist

**EF Migration:**
- Generate migration for all schema changes
- Verify migration applies cleanly on existing data
- Set `IsDeactivated = false` default for existing users

**Manual verification:**
- [ ] User can deactivate their own account → items delisted, signed out
- [ ] User can log back in and reactivate → items re-listed
- [ ] Admin can deactivate a user with reason → user sees reason on deactivated page
- [ ] Admin-deactivated user CANNOT self-reactivate (sees "submit request" instead)
- [ ] Admin can reactivate a user
- [ ] User can delete account → PII anonymized, items deleted, rentals preserved
- [ ] Admin can delete a user → same as user-initiated delete
- [ ] Deletion blocked when active rentals exist
- [ ] Data export downloads complete JSON with all related data
- [ ] Cookie consent banner appears, preferences saved, reappears on clear
- [ ] Registration requires consent checkbox
- [ ] Privacy policy page renders in both languages
- [ ] Views show "Deleted User" instead of crashing for anonymized users
- [ ] Data retention service runs and cleans old records (test with shorter period)

---

## Key Files Reference

| File | Role |
|------|------|
| `Models/Domain/ApplicationUser.cs` | Add deactivation + consent fields |
| `Models/Domain/DeactivationSource.cs` | New enum |
| `Models/Domain/CookieConsent.cs` | New entity |
| `Infrastructure/Data/RentMateContext.cs` | DbSet + config |
| `Services/Interfaces/IAccountLifecycleService.cs` | New service interface |
| `Services/Implementations/AccountLifecycleService.cs` | Centralized account operations |
| `Services/Implementations/DataRetentionService.cs` | Retention background job |
| `Infrastructure/Filters/DeactivatedAccountFilter.cs` | Auth filter for deactivated users |
| `Areas/Identity/Pages/Account/Manage/Security.cshtml.cs` | Refactor to use service |
| `Controllers/Mvc/UsersController.cs` | Fix admin deletion |
| `Controllers/Mvc/AccountController.cs` | Deactivated page + reactivate |
| `Controllers/Mvc/PrivacyController.cs` | Privacy policy page |
| `Views/Account/Deactivated.cshtml` | Deactivated account page |
| `Views/Privacy/Index.cshtml` | Privacy policy content |
| `Views/Shared/_CookieConsent.cshtml` | Cookie banner partial |
| `wwwroot/js/cookie-consent.js` | Cookie consent JS |
| `Resources/en.json` / `Resources/sl.json` | All new localization keys |

## Separate Design Doc Required

**Account Reactivation Dispute System**: When admin deactivates a user, the user can request reactivation via the dispute system. This requires:
- New dispute category/type
- User submission flow
- Admin review flow
- SignalR notifications

This is explicitly out of scope for this plan and will be designed separately.

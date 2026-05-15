# Identity Account Settings Revamp

## Context

The current `Areas/Identity/Pages/Account/Manage/` directory has 15+ Razor pages, many broken or using legacy Bootstrap styling without localization. The username is set to the user's email and marked read-only. Two-factor authentication pages don't work (missing QR code JS, broken flows). ExternalLogins has a bug displaying success on failure. Several pages lack dark mode and localization.

**Goal:** Consolidate from 15 pages down to 3 well-built pages (Profile, Security, Payments), add new profile fields (bio, map-based location, preferences, notifications), make username editable and independent from email, add full dark mode + bilingual localization, and stub 2FA for future implementation.

---

## Page Structure

### Page 1: Profile (`/Identity/Account/Manage/Index`)
Editable user identity and preferences:
1. **Profile Picture** — camera-overlay upload with drag-drop, larger preview
2. **Identity** — Username (editable, unique validation), First Name, Last Name
3. **Bio / About Me** — textarea with character count (500 max)
4. **Location** — City dropdown (`CityData.Cities`) + Leaflet.js/OpenStreetMap map picker for Latitude/Longitude
5. **Contact** — Phone number
6. **Preferences** — Return policy toggle (`HasReturnPolicy`), Language preference dropdown (SL/EN, persisted + cookie set)
7. **Notifications** — Email notification toggles (rental requests, messages, reviews, status changes)

### Page 2: Security (`/Identity/Account/Manage/Security`) — NEW
Consolidates 13 old pages into sections:
1. **Email** — current email with verified/unverified badge, change email form, resend verification
2. **Password** — unified: has-password mode (old/new/confirm) or no-password mode (new/confirm) with show/hide toggles
3. **External Logins** — linked providers with unlink, link new providers (rebuilt in Tailwind, bug fixed)
4. **Two-Factor Authentication** — STUB: shield icon + "Coming Soon" badge + explanation text, no functional code
5. **Data & Privacy** — expand/collapse inline:
   - Download Data button (JSON export via reflection)
   - Delete Account (two-mode: anonymize vs full wipe, preserving existing excellent logic wholesale)

### Page 3: Payments (`/Identity/Account/Manage/PaymentMethods`) — LIGHT REVAMP
Existing page with fixes:
- Fix hard-coded URL → Razor-generated URL
- Add dark mode classes + Stripe dark theme
- Polish styling for consistency

---

## Implementation Steps

### Step 1: Database Migration — New ApplicationUser Fields

**Files:**
- `RentMate-Web/Models/Domain/ApplicationUser.cs`

**Add properties:**
```csharp
#region Preferences
public string PreferredLanguage { get; set; } = "sl";
#endregion

#region Notification Preferences
public bool NotifyOnRentalRequest { get; set; } = true;
public bool NotifyOnMessage { get; set; } = true;
public bool NotifyOnReview { get; set; } = true;
public bool NotifyOnRentalStatusChange { get; set; } = true;
#endregion
```

**Commands:**
```bash
dotnet ef migrations add AddUserPreferences --project RentMate-Web/RentMate.csproj
dotnet ef database update --project RentMate-Web/RentMate.csproj
```

**Verify:** `dotnet build RentMate.sln` succeeds.

---

### Step 2: Simplify Navigation — ManageNavPages + _ManageNav

**Files:**
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/ManageNavPages.cs`
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/_ManageNav.cshtml`

**ManageNavPages.cs:** Strip to 3 constants + 3 nav class helpers: `Index`, `Security`, `PaymentMethods`. Remove all others.

**_ManageNav.cshtml:** Rewrite to 3 nav items with full dark mode:
- Profile (`bi-person` → `./Index`)
- Security (`bi-shield-lock` → `./Security`)
- Payments (`bi-credit-card` → `./PaymentMethods`)

Remove the `SignInManager` inject and `hasExternalLogins` conditional.

---

### Step 3: Dark Mode for Manage Layout + Status Message

**Files:**
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/_Layout.cshtml`
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/_StatusMessage.cshtml`

Add `dark:` Tailwind variants to layout container, header, sidebar card, content card, and status message alerts.

---

### Step 4: Profile Page Code-Behind (Index.cshtml.cs)

**Files:**
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/Index.cshtml.cs`

**Expand `InputModel`:**
- `Username` (string, `[StringLength(50)]`, `[RegularExpression(@"^[a-zA-Z0-9._-]+$")]`)
- `Bio` (string, `[StringLength(500)]`)
- `Latitude` (double?), `Longitude` (double?)
- `HasReturnPolicy` (bool)
- `PreferredLanguage` (string)
- `NotifyOnRentalRequest`, `NotifyOnMessage`, `NotifyOnReview`, `NotifyOnRentalStatusChange` (bool)

**Add `CityCoordinatesJson` property:** Serialized `CityData.Cities` for Leaflet JS.

**Add `UpdateUsernameAsync` helper:** Check uniqueness via `UserManager.FindByNameAsync`, then `UserManager.SetUserNameAsync`. Add ModelState error if taken.

**Expand `UpdateBasicProfileFields`:** Include Bio, Latitude, Longitude, HasReturnPolicy, PreferredLanguage, notification prefs.

**Language side-effect:** When `PreferredLanguage` changes, also set the `.AspNetCore.Culture` cookie (same logic as `CultureController.SetLanguage`).

**Reuse:** `IFileUploadService` (existing), `CityData.Cities` (existing).

---

### Step 5: Profile Page View (Index.cshtml)

**Files:**
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/Index.cshtml`

Full rewrite with 7 sections, Tailwind + dark mode, Leaflet map integration.

**Leaflet map details:**
- CDN: `https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.{css,js}` (already used in `Views/Items/Details.cshtml`)
- City dropdown change → look up coordinates from `CityCoordinatesJson` → `map.setView()` + marker
- Map click → update hidden Latitude/Longitude inputs + move marker
- Initial state: user lat/lng → user city → Slovenia default (46.15, 14.99)
- Map div: `h-64 rounded-xl`

**Toggle switch pattern:** Tailwind checkbox + styled label (no JS library).

---

### Step 6: Security Page Code-Behind (Security.cshtml.cs) — NEW FILE

**Files:**
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/Security.cshtml.cs` (create via Bash first)

**Class: `SecurityModel : BaseIdentityPageModel`**

**Inject:** `IEmailSender`, `ILogger<SecurityModel>`, `RentMateContext`, `IFileUploadService`, `IPaymentService`, `IUserStore<ApplicationUser>`

**Properties:**
- `Email`, `IsEmailConfirmed`, `HasPassword`
- `CurrentLogins`, `OtherLogins`, `ShowRemoveButton`
- `HasActiveRentals`, `RequirePassword`

**Input Models:** `EmailInputModel`, `PasswordInputModel`, `DeleteInputModel`

**Handlers:**
1. `OnGetAsync()` — load all display data
2. `OnPostChangeEmailAsync()` — from Email.cshtml.cs
3. `OnPostSendVerificationEmailAsync()` — from Email.cshtml.cs
4. `OnPostChangePasswordAsync()` — unified ChangePassword + SetPassword
5. `OnPostRemoveLoginAsync(loginProvider, providerKey)` — from ExternalLogins.cshtml.cs, **FIX BUG: use `SetErrorMessage()` on failure**
6. `OnPostLinkLoginAsync(provider)` — from ExternalLogins.cshtml.cs
7. `OnGetLinkLoginCallbackAsync()` — from ExternalLogins.cshtml.cs
8. `OnPostDownloadDataAsync()` — from DownloadPersonalData.cshtml.cs
9. `OnPostDeleteAccountAsync()` — **preserve wholesale** from DeletePersonalData.cshtml.cs (all private helpers: `AnonymizeUserAccountAsync`, `DeleteAllUserDataAsync`, `CleanupAllUserReferencesAsync`, `CleanupCloudinaryImagesAsync`)

---

### Step 7: Security Page View (Security.cshtml) — NEW FILE

**Files:**
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/Security.cshtml` (create via Bash first)

5 sections with expand/collapse, full Tailwind + dark mode. Reference `DeletePersonalData.cshtml` for the delete section's excellent dark mode patterns.

**JS in `@section Scripts`:** Section expand/collapse, password show/hide toggle, delete mode toggle.

---

### Step 8: PaymentMethods Light Revamp

**Files:**
- `RentMate-Web/Areas/Identity/Pages/Account/Manage/PaymentMethods.cshtml`

1. Fix hardcoded URL → `const confirmSetupUrl = '@Url.Page("./PaymentMethods", "ConfirmSetup")';`
2. Add dark mode classes + Stripe `{ theme: 'night' }` when dark mode active
3. Polish button styles for consistency

Code-behind unchanged.

---

### Step 9: Decouple Username from Email in ConfirmEmailChange

**Files:**
- `RentMate-Web/Areas/Identity/Pages/Account/ConfirmEmailChange.cshtml.cs`

Remove lines 94-100 (`SetUserNameAsync(user, email)` call and its error handling). Username is now independent of email. Rename method from `ChangeEmailAndUsernameAsync` to `ConfirmEmailChangeAsync`.

---

### Step 10: Localization Keys

**Files:**
- `RentMate-Web/Resources/en.json`
- `RentMate-Web/Resources/sl.json`

Add ~50-60 new keys for Profile sections (Bio, Location, Preferences, Notifications, Username), Security sections (all section headers, 2FA stub text, Data & Privacy), and any missing strings. Keep alphabetically sorted. Use `node -e` for bulk merge.

---

### Step 11: Delete Legacy Pages

**Delete 26 files (13 page pairs):**
```
Areas/Identity/Pages/Account/Manage/Email.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/ChangePassword.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/SetPassword.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/ExternalLogins.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/TwoFactorAuthentication.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/EnableAuthenticator.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/Disable2fa.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/GenerateRecoveryCodes.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/ShowRecoveryCodes.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/ResetAuthenticator.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/PersonalData.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/DeletePersonalData.cshtml(.cs)
Areas/Identity/Pages/Account/Manage/DownloadPersonalData.cshtml(.cs)
```

Grep codebase for any remaining references to deleted pages and fix.

**Verify:** `dotnet build RentMate.sln` succeeds.

---

## Step Dependencies

```
Step 1 (Migration)
  ├── Step 2 (Nav) ── Step 3 (Layout dark mode)
  ├── Step 4 (Profile .cs) ── Step 5 (Profile .cshtml)
  │                        └── Step 9 (ConfirmEmailChange)
  ├── Step 6 (Security .cs) ── Step 7 (Security .cshtml)
  └── Step 8 (Payments revamp)

Steps 5, 7, 8 ──► Step 10 (Localization)
Steps 6, 7    ──► Step 11 (Delete legacy)
```

---

## Bug Fixes Included

1. **ExternalLogins.cshtml.cs line 78:** `SetSuccessMessage` on failure → `SetErrorMessage` (fixed in Step 6)
2. **PaymentMethods.cshtml line 189:** Hard-coded URL → Razor-generated (fixed in Step 8)
3. **ConfirmEmailChange.cshtml.cs line 95:** Username synced to email → decoupled (fixed in Step 9)

---

## Key Reuse Points

| Existing Code | Location | Reused In |
|---|---|---|
| `BaseIdentityPageModel` | `Areas/Identity/Pages/BaseIdentityPageModel.cs` | All pages |
| `IFileUploadService` | `Services/Interfaces/IFileUploadService.cs` | Profile (picture), Security (delete cleanup) |
| `IPaymentService` | `Services/Interfaces/IPaymentService.cs` | Payments, Security (delete cleanup) |
| `CityData.Cities` | `RentMate.Shared/Helpers/CityData.cs` | Profile (city dropdown + map coordinates) |
| Leaflet 1.9.4 CDN | `Views/Items/Details.cshtml` lines 8, 351 | Profile (map picker) |
| Delete account logic | `DeletePersonalData.cshtml.cs` lines 140-310 | Security (preserved wholesale) |
| Email change logic | `Email.cshtml.cs` | Security |
| Password change logic | `ChangePassword.cshtml.cs` + `SetPassword.cshtml.cs` | Security (unified) |
| External login logic | `ExternalLogins.cshtml.cs` | Security (bug fixed) |
| Download data logic | `DownloadPersonalData.cshtml.cs` | Security |

---

## Verification

After all steps:
1. `dotnet build RentMate.sln` — must compile clean
2. Navigate to `/Identity/Account/Manage` — Profile page loads with all 7 sections
3. Edit username → save → verify uniqueness validation works
4. Change city → map centers → click map → coordinates update
5. Navigate to Security → verify email section, password section, external logins, 2FA stub, data privacy
6. Test expand/collapse on Data & Privacy section
7. Test delete account flow (both modes) on a test account
8. Navigate to Payments → verify Stripe card add/remove still works
9. Toggle dark mode → all 3 pages render correctly
10. Switch language (SL↔EN) → all text localizes
11. Grep for references to deleted pages → none remain

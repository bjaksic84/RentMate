# Item Details Page — Audit & Redesign Spec

## Context

The item details page (`/Items/Details/{id}`) is the most important conversion page in RentMate — it's where users decide whether to rent. An audit of the current implementation revealed 17 issues across bugs, UX problems, and unnecessary elements. This spec addresses all 17 items plus design polish improvements to the partials being touched.

**Prompted by:** Visual audit of the live page (screenshots reviewed 2026-03-27).

---

## Page Layout: Before → After

### Current order (main column):
1. Hero (Title, Rating, Location)
2. Gallery
3. **Owner Card** ← interrupts item flow
4. About This Item
5. Accessories
6. Rental Policies
7. Availability Calendar (2 months)
8. Reviews
9. Location Map
10. **FAQ** ← generic, not item-specific
11. Similar Items

### New order (main column):
1. Hero (Title, Rating, Location)
2. Gallery
3. About This Item
4. Accessories
5. Rental Policies (with semantic color polish)
6. **Owner Card** ← moved here, after item context
7. Availability Calendar (**single month**, compact)
8. Reviews (with "Rent to review" CTA)
9. Location Map
10. Similar Items

**Removed:** FAQ section, Safety Tips sidebar box.

### Sidebar: Before → After

**Before:** Price + Deposit + "Secure payment via Stripe" + "Secure payment and rental" (redundant) + Rent Now + 1 Reviews / 0 Rentals (confusing item-level stats) + Safety Tips box

**After:** Price/day + star rating + Deposit line + "Secure payment via Stripe" (localized) + Rent Now button + Owner mini-card (avatar, name, rating, completed rentals)

---

## All Changes — Detailed

### Bug Fixes

#### 1. "Member since2026" missing space
**File:** `Views/Items/Partials/_ItemOwnerCard.cshtml`
**Fix:** Add a space between the "Member since" label and the year value in the Razor template.

#### 2. Sidebar stats mismatch (item-level vs owner-level)
**File:** `Views/Items/Partials/_ItemBookingCard.cshtml`
**Fix:** Remove the "X Reviews / X Rentals" item-level stats section entirely. Replace with owner mini-card (see change #12).

#### 3. "Secure payment and rental" redundant text
**File:** `Views/Items/Partials/_ItemBookingCard.cshtml`
**Fix:** Remove the second security line ("Secure payment and rental" with shield icon). Keep only "Secure payment via Stripe".

#### 4. Accessory description showing meaningless "2"
**File:** `Views/Items/Partials/_ItemAccessories.cshtml`
**Fix:** Only render the description `<p>` element if `!string.IsNullOrWhiteSpace(accessory.Description)`. If description is blank/null, show only the name and price. This prevents meaningless content like "2" from appearing.

#### 5. Hardcoded strings not localized
**Files:** `_ItemBookingCard.cshtml`, `_ItemLocation.cshtml`, `_ItemPolicies.cshtml`
**Fix:** Replace all hardcoded English strings with `@Localizer["KeyName"]` calls. Add corresponding keys to both `Resources/en.json` and `Resources/sl.json` (alphabetically sorted).

Strings to localize:
- "Secure payment via Stripe" → `SecurePaymentViaStripe`
- "Always meet in a public place" → already localized (verify)
- "Inspect the item before renting" → already localized (verify)
- "Use in-app payments only" → already localized (verify)
- "Free cancellation before rental start date" → `FreeCancellationPolicy`
- "Platform policy" → `PlatformPolicy`
- "For safety and privacy, we only show the approximate area..." → `LocationPrivacyNotice`
- "You will receive the exact pickup address after booking confirmation." → `LocationAfterBooking`
- "Approximate location" → `ApproximateLocation`

### Structural Changes

#### 6. Move Owner Card below Rental Policies
**File:** `Views/Items/Details.cshtml`
**Change:** Reorder the partial references in the main column. Move `_ItemOwnerCard` from after `_ItemGallery` to after `_ItemPolicies`.

New main column order:
```
@Html.Partial("Partials/_ItemHero")
@Html.Partial("Partials/_ItemGallery")
<!-- About section (inline) -->
@Html.Partial("Partials/_ItemAccessories")
@Html.Partial("Partials/_ItemPolicies")
@Html.Partial("Partials/_ItemOwnerCard")
@Html.Partial("Partials/_ItemAvailability")
@Html.Partial("Partials/_ItemReviews")
@Html.Partial("Partials/_ItemLocation")
@Html.Partial("Partials/_ItemSimilar")
```

#### 7. Remove FAQ section
**File:** `Views/Items/Details.cshtml`
**Change:** Remove the `_ItemFaq` partial reference. The partial file (`_ItemFaq.cshtml`) can remain but is no longer rendered.

#### 8. Remove Safety Tips from sidebar
**File:** `Views/Items/Partials/_ItemBookingCard.cshtml`
**Change:** Remove the entire Safety Tips card section (the 3 bullet points with icons).

#### 9. Compact calendar to single month
**File:** `wwwroot/js/item-details.js` (calendar rendering section, ~lines 482-592)
**Change:** Render only 1 month at a time instead of 2 side-by-side. Keep the prev/next navigation arrows. The calendar container will be narrower and take less vertical space.

#### 10. Add CTA to "Rent this item to leave a review"
**File:** `Views/Items/Partials/_ItemReviews.cshtml`
**Change:** Replace the plain text "Rent this item to leave a review" with a styled card that includes a "Rent Now" button/link that triggers the booking modal (`onclick="openRentModal()"`).

#### 11. Remove duplicate security line from sidebar
Already covered in bug fix #3.

#### 12. Add Owner mini-card to sidebar
**File:** `Views/Items/Partials/_ItemBookingCard.cshtml`
**Change:** Below the Rent Now button, add a compact owner row:
- Small circular avatar (28-32px) with initials or profile picture
- Owner display name (bold)
- Star rating + completed rental count on a secondary line
- Entire row links to owner profile

Data already available in the ViewModel: `OwnerName`, `OwnerProfilePictureUrl`, `OwnerAverageRating`, `OwnerCompletedRentals`, `OwnerId`.

#### 13. Remove sidebar item-level stats
Already covered in bug fix #2.

### Design Polish

#### 14. Policies — semantic color for positive signals
**File:** `Views/Items/Partials/_ItemPolicies.cshtml`
**Change:** Apply success/positive styling to favorable policies:
- "Free cancellation" → green accent (emerald-500/10 bg, emerald-700 text for icon/label)
- "No deposit required" → green accent
- "Auto-approved" extensions → green accent
- "No limit" on duration → green accent
- Neutral/informational values keep current blue/gray styling
- Actual deposit amounts and manual-approval keep neutral styling

#### 15. Share button — add text label on desktop
**File:** `Views/Items/Partials/_ItemHero.cshtml`
**Change:** On desktop (`hidden lg:inline`), add "Share" text next to the share icon. Keep icon-only on mobile.

#### 16. Booking card — tighten visual hierarchy
**File:** `Views/Items/Partials/_ItemBookingCard.cshtml`
**Change:**
- Ensure price + star rating are on the same line (already are, verify)
- Tighten padding/margins so the card is compact
- Rent Now button: ensure it uses the gradient button pattern (`bg-gradient-to-r from-blue-600 to-blue-500`) with proper hover state
- Add a subtle divider (thin border) between the booking section and the owner mini-card

#### 17. Review "Rent to review" CTA card styling
**File:** `Views/Items/Partials/_ItemReviews.cshtml`
**Change:** Style the "rent to review" prompt as an inviting card rather than plain text:
- Light blue/indigo background
- Centered text with a friendly message
- "Rent Now" button below the message (triggers booking modal)

---

## Files Modified

| File | Changes |
|------|---------|
| `Views/Items/Details.cshtml` | Reorder partials, remove `_ItemFaq` reference |
| `Views/Items/Partials/_ItemHero.cshtml` | Share button text label on desktop |
| `Views/Items/Partials/_ItemOwnerCard.cshtml` | Fix "Member since" spacing |
| `Views/Items/Partials/_ItemBookingCard.cshtml` | Remove stats, remove safety tips, remove duplicate security text, add owner mini-card, tighten styling |
| `Views/Items/Partials/_ItemAccessories.cshtml` | Guard empty/numeric-only descriptions |
| `Views/Items/Partials/_ItemPolicies.cshtml` | Semantic color for positive policies, localize hardcoded strings |
| `Views/Items/Partials/_ItemReviews.cshtml` | "Rent to review" CTA card with button |
| `Views/Items/Partials/_ItemAvailability.cshtml` | No structural change (calendar change is in JS) |
| `Views/Items/Partials/_ItemLocation.cshtml` | Localize privacy notice strings |
| `wwwroot/js/item-details.js` | Compact calendar to single month |
| `Resources/en.json` | Add new localization keys |
| `Resources/sl.json` | Add new localization keys (Slovenian translations) |

## Files NOT Modified

| File | Reason |
|------|--------|
| `Views/Items/Partials/_ItemFaq.cshtml` | Kept on disk, just unreferenced. No deletion needed. |
| `Views/Items/Partials/_ItemGallery.cshtml` | No changes identified |
| `Views/Items/Partials/_ItemSimilar.cshtml` | No changes identified |
| `Views/Items/Partials/_ItemMobileBookingBar.cshtml` | No changes identified |
| `Views/Shared/_RentModal.cshtml` | No changes identified |
| `Controllers/Mvc/ItemsController.cs` | ViewModel already provides all needed data |
| `Models/ViewModels/ItemDetailsViewModel.cs` | No new fields needed |

---

## Localization Keys to Add

### en.json
```json
"ApproximateLocation": "Approximate location",
"FreeCancellationPolicy": "Free cancellation before rental start date",
"LocationAfterBooking": "You will receive the exact pickup address after booking confirmation.",
"LocationPrivacyNotice": "For safety and privacy, we only show the approximate area where the item is located.",
"PlatformPolicy": "Platform policy",
"RentToReview": "Rent this item to leave a review",
"RentToReviewCta": "Rent Now",
"SecurePaymentViaStripe": "Secure payment via Stripe"
```

### sl.json
```json
"ApproximateLocation": "Približna lokacija",
"FreeCancellationPolicy": "Brezplačna odpoved pred začetkom najema",
"LocationAfterBooking": "Natančen naslov za prevzem boste prejeli po potrditvi rezervacije.",
"LocationPrivacyNotice": "Zaradi varnosti in zasebnosti prikazujemo le približno območje, kjer se predmet nahaja.",
"PlatformPolicy": "Politika platforme",
"RentToReview": "Najemite ta predmet, da pustite oceno",
"RentToReviewCta": "Najemi zdaj",
"SecurePaymentViaStripe": "Varno plačilo prek Stripe"
```

---

## Verification

1. **Visual check:** Load `/Items/Details/{id}` for an item with:
   - Multiple images (verify gallery still works)
   - At least 1 review (verify reviews section)
   - At least 1 accessory (verify accessories render correctly)
   - A deposit amount (verify deposit line in sidebar)
   - An owner with completed rentals (verify owner mini-card in sidebar)
2. **Layout order:** Confirm sections appear in new order: Hero → Gallery → About → Accessories → Policies → Owner → Calendar → Reviews → Map → Similar
3. **Sidebar:** Confirm only Price + Deposit + Stripe + Rent Now + Owner mini-card appear. No stats, no safety tips, no duplicate text.
4. **Calendar:** Confirm single month with prev/next navigation
5. **Localization:** Switch to Slovenian (sl) and verify all previously-hardcoded strings now display in Slovenian
6. **Dark mode:** Toggle dark mode and verify all modified sections render correctly
7. **Mobile:** Resize to mobile viewport — confirm mobile booking bar still works, owner card responsive
8. **Booking modal:** Click "Rent Now" from both sidebar and "rent to review" CTA — confirm modal opens correctly
9. **FAQ gone:** Confirm no FAQ section appears on the page

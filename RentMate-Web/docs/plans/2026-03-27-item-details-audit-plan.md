# Item Details Page Audit & Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 5 bugs, apply 8 structural changes, and polish 4 design elements on the item details page to improve conversion flow and remove clutter.

**Architecture:** All changes are in Razor partials and one JS file — no backend/model/controller changes needed. The page already has all required data in the ViewModel. Localization keys already exist in both `en.json` and `sl.json`.

**Tech Stack:** ASP.NET Core Razor views, Tailwind CSS (CDN), vanilla JavaScript, Bootstrap Icons

**Spec:** `docs/plans/2026-03-27-item-details-audit-design.md`

---

### Task 1: Reorder page layout and remove FAQ

**Files:**
- Modify: `RentMate-Web/Views/Items/Details.cshtml`

- [ ] **Step 1: Move owner card partial and remove FAQ reference**

In `Details.cshtml`, the main content column (lines 24-44) currently has this partial order:

```cshtml
<partial name="Partials/_ItemHero" model="Model" />
<partial name="Partials/_ItemGallery" model="Model" />
<partial name="Partials/_ItemOwnerCard" model="Model" />

@if (!string.IsNullOrEmpty(Model.Description))
{
    <!-- About section -->
}

<partial name="Partials/_ItemAccessories" model="Model" />
<partial name="Partials/_ItemPolicies" model="Model" />
<partial name="Partials/_ItemAvailability" model="Model" />
<partial name="Partials/_ItemReviews" model="Model" />
<partial name="Partials/_ItemLocation" model="Model" />
<partial name="Partials/_ItemFaq" model="Model" />
<partial name="Partials/_ItemSimilar" model="Model" />
```

Change it to:

```cshtml
<partial name="Partials/_ItemHero" model="Model" />
<partial name="Partials/_ItemGallery" model="Model" />

@if (!string.IsNullOrEmpty(Model.Description))
{
    <div class="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 p-6">
        <h2 class="font-heading text-xl font-bold text-slate-900 dark:text-white mb-4">@Localizer["About this item"]</h2>
        <p class="text-slate-600 dark:text-slate-300 whitespace-pre-line leading-relaxed">@Model.Description</p>
    </div>
}

<partial name="Partials/_ItemAccessories" model="Model" />
<partial name="Partials/_ItemPolicies" model="Model" />
<partial name="Partials/_ItemOwnerCard" model="Model" />
<partial name="Partials/_ItemAvailability" model="Model" />
<partial name="Partials/_ItemReviews" model="Model" />
<partial name="Partials/_ItemLocation" model="Model" />
<partial name="Partials/_ItemSimilar" model="Model" />
```

Key changes:
- `_ItemOwnerCard` moves from after `_ItemGallery` to after `_ItemPolicies`
- `_ItemFaq` line is deleted entirely
- About section stays in the same inline position (just after gallery)

- [ ] **Step 2: Build and verify**

Run: `dotnet build RentMate-Web/RentMate.csproj`
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add RentMate-Web/Views/Items/Details.cshtml
git commit -m "refactor(item-details): reorder sections and remove FAQ

Move owner card below policies for item-first flow.
Remove generic FAQ section (not item-specific).

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 2: Overhaul the booking card sidebar

**Files:**
- Modify: `RentMate-Web/Views/Items/Partials/_ItemBookingCard.cshtml`

This task addresses spec items #2, #3, #8, #12, #13, #16: remove item-level stats, remove "Secure payment and rental" duplicate, remove safety tips, add owner mini-card, tighten visual hierarchy.

- [ ] **Step 1: Replace the entire booking card**

Replace the full contents of `_ItemBookingCard.cshtml` with:

```cshtml
@model RentMate.Models.ViewModels.ItemDetailsViewModel
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer
@inject RentMate.Services.Interfaces.ICurrencyService CurrencyService

@{
    var firstInitial = !string.IsNullOrEmpty(Model.OwnerName) ? Model.OwnerName[0].ToString().ToUpper() : "?";
}

<div class="sticky top-24">
    <div class="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-md p-5">
        <!-- Price + Rating -->
        <div class="flex items-end justify-between mb-4">
            <div>
                <span class="text-3xl font-bold text-slate-900 dark:text-white">@CurrencyService.Format(Model.Price)</span>
                <span class="text-slate-500 dark:text-slate-400"> / @Localizer["day"]</span>
            </div>
            @if (Model.AverageRating.HasValue && Model.AverageRating > 0)
            {
                <div class="flex items-center gap-1 text-sm">
                    <i class="bi bi-star-fill text-amber-400"></i>
                    <span class="font-semibold text-slate-900 dark:text-white">@Model.AverageRating.Value.ToString("0.0")</span>
                </div>
            }
        </div>

        <!-- Deposit info -->
        @if (Model.DepositAmount.HasValue && Model.DepositAmount.Value > 0)
        {
            <div class="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400 mb-4 p-3 bg-slate-50 dark:bg-slate-700/50 rounded-xl">
                <i class="bi bi-shield-check text-blue-500"></i>
                <span>+ @CurrencyService.Format(Model.DepositAmount.Value) @Localizer["Deposit"] (@Localizer["refundable"])</span>
            </div>
        }

        <!-- Trust signal -->
        <div class="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400 mb-5">
            <i class="bi bi-lock-fill text-green-500"></i>
            <span>@Localizer["Secure payment via Stripe"]</span>
        </div>

        <!-- Rent Now button -->
        @if (!Model.IsOwner)
        {
            <button type="button"
                    class="w-full py-3.5 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white font-bold rounded-xl shadow-sm hover:shadow-md transition-all duration-300"
                    onclick="document.getElementById('rentModal-@Model.ItemId').classList.remove('hidden')">
                @Localizer["Rent Now"]
            </button>
        }

        <!-- Owner Mini-Card -->
        <div class="mt-5 pt-5 border-t border-slate-200 dark:border-slate-700">
            <a asp-controller="Profile" asp-action="Details" asp-route-id="@Model.OwnerId"
               class="flex items-center gap-3 group">
                @if (!string.IsNullOrEmpty(Model.OwnerProfilePictureUrl))
                {
                    <img src="@Model.OwnerProfilePictureUrl" alt="@Model.OwnerName"
                         class="w-10 h-10 rounded-full object-cover shrink-0" />
                }
                else
                {
                    <div class="w-10 h-10 rounded-full bg-gradient-to-br from-blue-500 to-blue-600 flex items-center justify-center shrink-0">
                        <span class="text-white text-sm font-semibold">@firstInitial</span>
                    </div>
                }
                <div class="min-w-0">
                    <div class="font-semibold text-sm text-slate-900 dark:text-white group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors truncate">
                        @Model.OwnerName
                    </div>
                    <div class="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
                        @if (Model.OwnerAverageRating > 0)
                        {
                            <i class="bi bi-star-fill text-amber-400" style="font-size: 0.6rem;"></i>
                            <span>@Model.OwnerAverageRating.ToString("F1")</span>
                            <span class="text-slate-300 dark:text-slate-600">&middot;</span>
                        }
                        <span>@Model.OwnerCompletedRentals @Localizer["Completed Rentals"]</span>
                    </div>
                </div>
                <i class="bi bi-chevron-right text-slate-400 dark:text-slate-500 ml-auto text-xs"></i>
            </a>
        </div>
    </div>
</div>
```

Removed: item-level stats grid, "Secure payment and rental" line, Safety Tips box, Trusted Host badge (already shown on owner card).
Added: Owner mini-card with avatar, name, rating, rental count.
Tightened: reduced padding from p-6 to p-5, reduced mb-6 to mb-4/mb-5, button py-4 to py-3.5.

- [ ] **Step 2: Build and verify**

Run: `dotnet build RentMate-Web/RentMate.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add RentMate-Web/Views/Items/Partials/_ItemBookingCard.cshtml
git commit -m "refactor(booking-card): slim sidebar with owner mini-card

Remove item-level stats, duplicate security text, and safety tips.
Add compact owner row (avatar, name, rating, rentals) below CTA.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 3: Fix "Member since" spacing in owner card

**Files:**
- Modify: `RentMate-Web/Views/Items/Partials/_ItemOwnerCard.cshtml`

- [ ] **Step 1: Add a space before the date value**

In `_ItemOwnerCard.cshtml` lines 47-55, the current code is:

```cshtml
<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
    @if (Model.OwnerMemberSince.Year >= 2000)
    {
        @Localizer["Member since"] @Model.OwnerMemberSince.ToString("MMMM yyyy")
    }
    else
    {
        @Localizer["Member since"] @DateTime.UtcNow.Year
    }
</p>
```

Razor strips whitespace between the localizer call and the date. Fix by using explicit string interpolation:

```cshtml
<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
    @if (Model.OwnerMemberSince.Year >= 2000)
    {
        @Localizer["Member since"] <text> </text>@Model.OwnerMemberSince.ToString("MMMM yyyy")
    }
    else
    {
        @Localizer["Member since"] <text> </text>@DateTime.UtcNow.Year
    }
</p>
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build RentMate-Web/RentMate.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add RentMate-Web/Views/Items/Partials/_ItemOwnerCard.cshtml
git commit -m "fix(owner-card): add missing space in 'Member since' text

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 4: Guard empty accessory descriptions

**Files:**
- Modify: `RentMate-Web/Views/Items/Partials/_ItemAccessories.cshtml`

- [ ] **Step 1: Change IsNullOrEmpty to IsNullOrWhiteSpace**

In `_ItemAccessories.cshtml` line 26, change:

```cshtml
@if (!string.IsNullOrEmpty(accessory.Description))
```

to:

```cshtml
@if (!string.IsNullOrWhiteSpace(accessory.Description))
```

This prevents descriptions that are just whitespace or meaningless short strings from rendering.

- [ ] **Step 2: Commit**

```bash
git add RentMate-Web/Views/Items/Partials/_ItemAccessories.cshtml
git commit -m "fix(accessories): use IsNullOrWhiteSpace for description guard

Prevents empty/whitespace-only descriptions from rendering.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 5: Add semantic color to positive rental policies

**Files:**
- Modify: `RentMate-Web/Views/Items/Partials/_ItemPolicies.cshtml`

- [ ] **Step 1: Replace the policies grid with semantic-colored version**

Replace the full contents of `_ItemPolicies.cshtml` with:

```cshtml
@model RentMate.Models.ViewModels.ItemDetailsViewModel
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer
@inject RentMate.Services.Interfaces.ICurrencyService CurrencyService

@{
    var noLimit = !Model.MaxRentalDays.HasValue;
    var autoApprove = Model.AutoApproveExtensions;
    var noDeposit = !Model.DepositAmount.HasValue || Model.DepositAmount.Value <= 0;
}

<div class="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 p-6">
    <!-- Section Heading -->
    <h3 class="font-heading text-lg font-semibold text-slate-900 dark:text-white flex items-center gap-2 mb-4">
        <i class="bi bi-clipboard-check text-blue-500"></i>
        @Localizer["Rental Policies"]
    </h3>

    <!-- Policy Grid -->
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <!-- Max Rental Duration -->
        <div class="p-4 @(noLimit ? "bg-emerald-50 dark:bg-emerald-900/20" : "bg-slate-50 dark:bg-slate-700/50") rounded-xl">
            <div class="flex items-center gap-2 mb-2">
                <i class="bi bi-calendar-range @(noLimit ? "text-emerald-500 dark:text-emerald-400" : "text-blue-500 dark:text-blue-400")"></i>
                <span class="text-sm font-medium text-slate-500 dark:text-slate-400">@Localizer["Max Rental Duration"]</span>
            </div>
            <div class="text-sm font-semibold @(noLimit ? "text-emerald-700 dark:text-emerald-400" : "text-slate-900 dark:text-white")">
                @if (Model.MaxRentalDays.HasValue)
                {
                    @($"{Model.MaxRentalDays.Value} {Localizer["days"].Value}")
                }
                else
                {
                    @Localizer["No limit"]
                }
            </div>
        </div>

        <!-- Extension Policy -->
        <div class="p-4 @(autoApprove ? "bg-emerald-50 dark:bg-emerald-900/20" : "bg-slate-50 dark:bg-slate-700/50") rounded-xl">
            <div class="flex items-center gap-2 mb-2">
                <i class="bi bi-arrow-repeat @(autoApprove ? "text-emerald-500 dark:text-emerald-400" : "text-blue-500 dark:text-blue-400")"></i>
                <span class="text-sm font-medium text-slate-500 dark:text-slate-400">@Localizer["Extension Policy"]</span>
            </div>
            <div class="text-sm font-semibold @(autoApprove ? "text-emerald-700 dark:text-emerald-400" : "text-slate-900 dark:text-white")">
                @if (Model.AutoApproveExtensions)
                {
                    @Localizer["Auto-approved"]
                }
                else
                {
                    @Localizer["Requires owner approval"]
                }
            </div>
        </div>

        <!-- Deposit -->
        <div class="p-4 @(noDeposit ? "bg-emerald-50 dark:bg-emerald-900/20" : "bg-slate-50 dark:bg-slate-700/50") rounded-xl">
            <div class="flex items-center gap-2 mb-2">
                <i class="bi bi-shield-check @(noDeposit ? "text-emerald-500 dark:text-emerald-400" : "text-blue-500 dark:text-blue-400")"></i>
                <span class="text-sm font-medium text-slate-500 dark:text-slate-400">@Localizer["Deposit"]</span>
            </div>
            <div class="text-sm font-semibold @(noDeposit ? "text-emerald-700 dark:text-emerald-400" : "text-slate-900 dark:text-white")">
                @if (Model.DepositAmount.HasValue && Model.DepositAmount.Value > 0)
                {
                    @CurrencyService.Format(Model.DepositAmount.Value)
                }
                else
                {
                    @Localizer["No deposit required"]
                }
            </div>
        </div>

        <!-- Cancellation Policy (always positive) -->
        <div class="p-4 bg-emerald-50 dark:bg-emerald-900/20 rounded-xl">
            <div class="flex items-center gap-2 mb-2">
                <i class="bi bi-x-circle text-emerald-500 dark:text-emerald-400"></i>
                <span class="text-sm font-medium text-slate-500 dark:text-slate-400">@Localizer["Cancellation Policy"]</span>
            </div>
            <div class="text-sm font-semibold text-emerald-700 dark:text-emerald-400">
                @Localizer["Free cancellation before rental start date"]
            </div>
            <div class="text-xs text-slate-400 dark:text-slate-500 mt-1">@Localizer["Platform policy"]</div>
        </div>
    </div>
</div>
```

Logic: Positive policies (no limit, auto-approved, no deposit, free cancellation) get emerald green background and text. Neutral/restrictive policies keep the default slate styling.

- [ ] **Step 2: Build and verify**

Run: `dotnet build RentMate-Web/RentMate.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add RentMate-Web/Views/Items/Partials/_ItemPolicies.cshtml
git commit -m "style(policies): add green accent for positive policy values

No limit, auto-approved, no deposit, and free cancellation get
emerald styling. Restrictive values keep neutral slate colors.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 6: Add share button text label and style "Rent to review" CTA

**Files:**
- Modify: `RentMate-Web/Views/Items/Partials/_ItemHero.cshtml`
- Modify: `RentMate-Web/Views/Items/Partials/_ItemReviews.cshtml`

- [ ] **Step 1: Add "Share" text to the share button on desktop**

In `_ItemHero.cshtml` lines 57-62, replace the share button:

```cshtml
<button type="button"
        id="shareBtn"
        title="@Localizer["Share"]"
        class="w-10 h-10 flex items-center justify-center rounded-full border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 hover:text-blue-600 dark:hover:text-blue-400 hover:border-blue-300 dark:hover:border-blue-500 transition-all">
    <i class="bi bi-share text-lg"></i>
</button>
```

with:

```cshtml
<button type="button"
        id="shareBtn"
        title="@Localizer["Share"]"
        class="h-10 flex items-center justify-center gap-2 px-3 lg:px-4 rounded-full border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 hover:text-blue-600 dark:hover:text-blue-400 hover:border-blue-300 dark:hover:border-blue-500 transition-all">
    <i class="bi bi-share text-lg"></i>
    <span class="hidden lg:inline text-sm font-medium">@Localizer["Share"]</span>
</button>
```

- [ ] **Step 2: Style the "Rent to review" prompt as a CTA card**

In `_ItemReviews.cshtml` lines 74-80, replace:

```cshtml
else if (Model.IsSignedIn && !Model.CanReview)
{
    <div class="mb-8 p-4 bg-slate-50 dark:bg-slate-700/50 rounded-xl">
        <span class="text-slate-500 dark:text-slate-400 text-sm">
            @Localizer["Rent this item to leave a review"]
        </span>
    </div>
}
```

with:

```cshtml
else if (Model.IsSignedIn && !Model.CanReview && !Model.IsOwner)
{
    <div class="mb-8 p-6 bg-blue-50 dark:bg-blue-900/20 rounded-2xl border border-blue-100 dark:border-blue-800/50 text-center">
        <i class="bi bi-chat-square-heart text-3xl text-blue-400 dark:text-blue-500 mb-2"></i>
        <p class="text-slate-700 dark:text-slate-300 text-sm mb-4">
            @Localizer["Rent this item to leave a review"]
        </p>
        <button type="button"
                class="px-6 py-2.5 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white font-semibold rounded-xl hover:shadow-md transition-all text-sm"
                onclick="document.getElementById('rentModal-@Model.ItemId').classList.remove('hidden')">
            @Localizer["Rent Now"]
        </button>
    </div>
}
```

Note: Added `!Model.IsOwner` guard since owners can't rent their own items and shouldn't see a Rent Now CTA.

- [ ] **Step 3: Build and verify**

Run: `dotnet build RentMate-Web/RentMate.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add RentMate-Web/Views/Items/Partials/_ItemHero.cshtml RentMate-Web/Views/Items/Partials/_ItemReviews.cshtml
git commit -m "style(details): share button label + rent-to-review CTA card

Add 'Share' text on desktop next to icon. Restyle rent-to-review
prompt as an inviting blue card with Rent Now button.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 7: Compact availability calendar to single month

**Files:**
- Modify: `RentMate-Web/wwwroot/js/item-details.js`

- [ ] **Step 1: Change the calendar render function to show 1 month**

In `item-details.js`, find the `render()` function inside `initAvailabilityCalendar()` (around line 561). Replace:

```javascript
function render() {
    var nextMonth = currentMonth + 1;
    var nextYear = currentYear;
    if (nextMonth > 11) { nextMonth = 0; nextYear++; }

    var html = '<div class="flex items-center justify-between mb-4">';
    html += '<button type="button" onclick="window._availCalPrev()" class="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 transition-colors" aria-label="Previous month"><i class="bi bi-chevron-left"></i></button>';
    html += '<button type="button" onclick="window._availCalNext()" class="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 transition-colors" aria-label="Next month"><i class="bi bi-chevron-right"></i></button>';
    html += '</div>';

    html += '<div class="grid grid-cols-1 md:grid-cols-2 gap-6">';
    html += '<div>' + renderMonth(currentYear, currentMonth) + '</div>';
    html += '<div>' + renderMonth(nextYear, nextMonth) + '</div>';
    html += '</div>';

    container.innerHTML = html;
}
```

with:

```javascript
function render() {
    var monthLabel = new Date(currentYear, currentMonth, 1)
        .toLocaleDateString(undefined, { month: 'long', year: 'numeric' });

    var html = '<div class="flex items-center justify-between mb-4">';
    html += '<button type="button" onclick="window._availCalPrev()" class="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 transition-colors" aria-label="Previous month"><i class="bi bi-chevron-left"></i></button>';
    html += '<div class="text-sm font-semibold text-slate-700 dark:text-slate-300">' + escHtml(monthLabel) + '</div>';
    html += '<button type="button" onclick="window._availCalNext()" class="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 transition-colors" aria-label="Next month"><i class="bi bi-chevron-right"></i></button>';
    html += '</div>';

    html += renderMonth(currentYear, currentMonth);

    container.innerHTML = html;
}
```

Key changes:
- Renders only 1 month instead of 2
- Month title is now between the nav arrows (centered) instead of inside `renderMonth`
- Removed the 2-column grid wrapper

- [ ] **Step 2: Remove the duplicate month title from renderMonth**

In the `renderMonth` function (around line 514), the first line of generated HTML is the month title. Remove it since we now render the title in the nav bar. Change:

```javascript
var monthName = firstDay.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });

var html = '<div class="text-center font-semibold text-slate-700 dark:text-slate-300 text-sm mb-3">' + escHtml(monthName) + '</div>';
html += '<div class="grid grid-cols-7 gap-0.5 mb-1">';
```

to:

```javascript
var html = '<div class="grid grid-cols-7 gap-0.5 mb-1">';
```

(Delete the `monthName` variable and the title div — no longer needed.)

- [ ] **Step 3: Commit**

```bash
git add RentMate-Web/wwwroot/js/item-details.js
git commit -m "refactor(calendar): compact to single month with nav

Show one month at a time with centered title between prev/next arrows.
Reduces vertical space taken by the availability section.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 8: Visual verification

- [ ] **Step 1: Start the app and verify all changes**

Run: `dotnet run --project RentMate-Web/RentMate.csproj`

Open `https://localhost:7280/Items/Details/11` (or any item with images, reviews, accessories, and deposit) and verify:

1. **Section order:** Hero → Gallery → About → Accessories → Policies → Owner Card → Calendar → Reviews → Map → Similar Items
2. **No FAQ section** on the page
3. **Sidebar:** Price + Deposit + "Secure payment via Stripe" + Rent Now + Owner mini-card (avatar, name, rating, rentals). No stats grid, no safety tips, no "Secure payment and rental"
4. **Owner card "Member since":** Has a space before the year (e.g., "Member since March 2026")
5. **Policies:** Positive values (No limit, Auto-approved, No deposit, Free cancellation) show in green; restrictive values show in neutral slate
6. **Calendar:** Single month with left/right nav arrows and centered month title. Prev/next works
7. **Share button:** Shows "Share" text label on desktop (lg), icon-only on mobile
8. **"Rent to review" CTA:** Blue card with icon + text + Rent Now button (only for signed-in non-owners who haven't rented)
9. **Dark mode:** Toggle and verify all modified sections render correctly
10. **Mobile:** Resize — mobile booking bar still works, layout is single-column, share button is icon-only
11. **Rent Now modal:** Click from sidebar and from "rent to review" CTA — both open the modal
12. **Slovenian:** Switch language — all text displays in Slovenian

- [ ] **Step 2: Final commit if any hot fixes were needed**

If any small fixes were needed during verification, commit them.

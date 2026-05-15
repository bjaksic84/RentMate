# Dashboard Redesign — Complete Overhaul

## Context

The current user dashboard is cluttered, unbalanced, and unintuitive. It mixes renter/owner contexts, has extremely dense rental cards showing everything at once (status, deposits, disputes, evidence, accessories, actions), and crams item management, favorites, and earnings into a narrow sidebar. This plan redesigns the dashboard from scratch with a 4-tab layout, attention-first card design, and matching the existing app visual tone.

**Backend remains untouched** — all changes are frontend-only (Razor views, Tailwind CSS, vanilla JS).

## Design Decisions

| Decision | Choice |
|----------|--------|
| Tab structure | 4 tabs: Home, Renting, Lending, History |
| Card design | Attention-first hybrid — urgent items float to top, normal rentals as compact grouped rows |
| Sidebar | Removed — content integrated into tabs |
| Home tab | Balanced mix — stats left, attention right, quick links below |
| Info density | Progressive disclosure — click rows to expand details |
| Visual tone | **Match existing app** — trust-blue palette, current card/button patterns, existing `theme.css`/`site.css` tokens. No visual redesign; full visual overhaul comes later. |

## Implementation Steps

> **One step per session turn** (per CLAUDE.md). Complete, verify, then stop for review.

---

### Step 1: Tab Shell + Tab JS

**Goal**: Replace the linear layout in `UserDashboard.cshtml` with a tab container. Add client-side tab switching with URL hash persistence.

**Files to modify**:
- `RentMate-Web/Views/Dashboard/UserDashboard.cshtml` — Rewrite orchestrator: keep header (greeting, admin link, add item), keep `@Html.AntiForgeryToken()` + toast container + `_Alert`, remove current stats/attention/two-column layout. Add tab nav bar (4 pill buttons with badge counts) and 4 `<div data-tab-panel="...">` containers. Modal partials (`_RentalModals`, `_DepositModals`, `_DisputeModals`) stay outside tabs. `DashboardConfig` bridge stays in `@section Scripts`.
- `RentMate-Web/wwwroot/js/dashboard.js` — Add `initTabs()`: reads `location.hash` for active tab (default `#home`), click handlers toggle panels + update hash + active button styling, `hashchange` listener for back/forward.

**Temporarily** load old partials inside tab panels so nothing breaks (renting tab gets `_MyRentals` + `_AttentionBanner`, lending gets `_RentingOut`, etc.).

**Localization keys**: `DashboardTabHome`, `DashboardTabRenting`, `DashboardTabLending`, `DashboardTabHistory`

**Verify**: Page loads, tabs switch, hash persists on reload, no JS errors, all old content accessible.

---

### Step 2: Home Tab (`_DashboardHome.cshtml`)

**Goal**: Build the Home tab with stats + attention panel + quick links.

**Files to create**:
- `RentMate-Web/Views/Dashboard/_DashboardHome.cshtml`

**Files to modify**:
- `RentMate-Web/Views/Dashboard/UserDashboard.cshtml` — Replace temp content in home tab panel with new partial
- `RentMate-Web/Views/Shared/_StatCard.cshtml` — Add dark mode classes (`dark:bg-slate-800`, `dark:text-white`, `dark:border-slate-700`)

**Content**:
- **Top row** (2-col grid on desktop, stacked on mobile):
  - Left: 2x2 stat cards — Renting count (`Model.MyRentals` active), Lending count (`Model.OwnerRentals` active), Monthly earnings (completed owner rentals this month), Trust score (`ViewBag.ProfileTrustScore`)
  - Right: Attention panel — overdue rentals, pending requests, pending extensions, disputes. Each item: color-coded left border, icon, title+subtitle, inline action button
- **Bottom row**: 4 quick-link cards — My Items (-> `#lending`), Favorites (-> `#renting`), List New Item (-> `/Items/Create`), My Profile (-> `/Profile`)

**Localization keys**: `MonthlyEarnings`, `TrustScore`, `NeedsAttention`, `NothingNeedsAttention`, `QuickLinks`, `ListNewItem`, `DashboardSubtitle`

**Verify**: Home tab shows stats, attention items with working action buttons, quick links navigate correctly. Dark mode works.

---

### Step 3: Renting Tab (`_DashboardRenting.cshtml`)

**Goal**: Build the Renting tab — the most complex step due to deposit/dispute UI migration.

**Files to create**:
- `RentMate-Web/Views/Dashboard/_DashboardRenting.cshtml`

**Files to modify**:
- `RentMate-Web/Views/Dashboard/UserDashboard.cshtml` — Replace temp content in renting tab
- `RentMate-Web/wwwroot/js/dashboard.js` — Add `initExpandableRows()` for click-to-expand + chevron rotation + `max-height` transition

**Content**:
- **Summary strip**: 3 inline stats (active count, pending count, daily cost total)
- **Attention section**: Overdue rental cards (rose border, extend button calling `openExtensionModal()`)
- **Grouped rows** (accordion-style):
  - "Active Rentals" section header
  - Compact rows: thumbnail (44x44), title, owner name, due date, price, status badge via `_StatusBadge`, expand chevron
  - **Expand panel** (hidden by default): Full detail — date range, deposit info, accessories list, countdown timer via `_CountdownTimer`, action buttons. **Critical**: migrate ALL deposit/dispute conditional rendering from current `_MyRentals.cshtml` (charged, disputed, counter-offered, escalated, resolved states) with identical `onclick` handler signatures for existing modal functions
  - "Pending Rentals" section header
  - Same row pattern for pending/accepted rentals
- **Favorites strip**: Horizontal scrollable row of compact chips (from `Model.FavoriteItems`) — thumbnail, title, price. Links to item detail.
- **Empty state**: `_EmptyState` partial

**Localization keys**: `ActiveRentals`, `PendingRentals`, `DailyCost`, `SavedFavorites`

**Verify**: All rental rows display correctly. Expand/collapse works with smooth animation. ALL existing actions work: extend, cancel, pay, dispute, accept charge, counter-offer response, escalate, add evidence, leave/edit review. Deposit status rendering matches current behavior. Favorites scroll horizontally.

---

### Step 4: Lending Tab (`_DashboardLending.cshtml`)

**Goal**: Build the Lending tab with owner-perspective rentals, earnings, and items grid.

**Files to create**:
- `RentMate-Web/Views/Dashboard/_DashboardLending.cshtml`

**Files to modify**:
- `RentMate-Web/Views/Dashboard/UserDashboard.cshtml` — Replace temp content in lending tab

**Content**:
- **Summary strip**: Rented out count, pending requests, daily income total
- **Attention section**: Pending rental requests with approve/decline buttons (same form actions as current `_AttentionBanner`). Pending extension requests with approve/decline.
- **Grouped rows**: Owner-perspective rentals with expand panels. Migrate all owner-side deposit/dispute rendering from `_RentingOut.cshtml` (deposit resolution, dispute response, counter-offer, maintain charge, early return). Same expand pattern as Step 3.
- **Earnings bar**: 3 stat cards in a row — Total earned, This month, Deposits held (from `Model.DepositSummary`)
- **My Items mini-grid**: 3-col grid of owned items (from `Model.ListingsOwned`). Each: thumbnail, title, price, listed/hidden dot, toggle button (`toggleListing()`), edit link, delete. Plus "Add Item" card linking to `/Items/Create`.

**Localization keys**: `RentedOut`, `PendingRequests`, `DailyIncome`, `EarningsOverview`, `TotalEarned`, `DepositsHeld`, `MyItems`

**Verify**: Owner-perspective rentals display and expand correctly. Approve/decline work. Toggle listing works. Deposit resolution modal opens correctly. Earnings display accurately.

---

### Step 5: History Tab (`_DashboardHistory.cshtml`)

**Goal**: Build the History tab with filterable rental history.

**Files to create**:
- `RentMate-Web/Views/Dashboard/_DashboardHistory.cshtml`

**Files to modify**:
- `RentMate-Web/Views/Dashboard/UserDashboard.cshtml` — Replace temp content in history tab
- `RentMate-Web/wwwroot/js/dashboard.js` — Add `initHistoryFilters()` for client-side chip filtering

**Content**:
- **Filter chips**: All, As Renter, As Owner, Completed, Cancelled — client-side toggle via `data-role` and `data-status` attributes on rows
- **Row list**: Compact rows — thumbnail, title, date range, role pill ("As Renter"/"As Owner"), price, status badge. Action buttons: Leave/Edit Review (renter + completed), Rent Again, Dispute History
- **Dispute rows**: Subtle yellow highlight for rentals with disputed deposits
- **Load more**: Show first 12, "Load More" reveals rest (all data already in ViewModel)
- **Empty state** for new users

**Localization keys**: `AllHistory`, `AsRenter`, `AsOwner`, `CompletedFilter`, `CancelledFilter`, `LoadMore`, `ShowingXofY`

**Verify**: Filters work correctly. Review modal opens. Dispute history links work. Load more reveals additional rows.

---

### Step 6: Cleanup + Final Polish

**Goal**: Remove old partials, clean up dead references, final responsiveness pass.

**Files to delete**:
- `RentMate-Web/Views/Dashboard/_AttentionBanner.cshtml`
- `RentMate-Web/Views/Dashboard/_MyRentals.cshtml`
- `RentMate-Web/Views/Dashboard/_RentingOut.cshtml`
- `RentMate-Web/Views/Dashboard/_RentalHistory.cshtml`
- `RentMate-Web/Views/Dashboard/_DashboardSidebar.cshtml`

**Files to keep unchanged**:
- `_RentalModals.cshtml`, `_DepositModals.cshtml`, `_DisputeModals.cshtml` — preserved as-is
- `dashboard-disputes.js` — preserved as-is

**Final checks**:
- Remove any remaining old partial references from `UserDashboard.cshtml`
- Mobile responsiveness: test all 4 tabs at 375px, 768px, 1024px, 1440px
- Dark mode: verify all new partials
- Verify SignalR `location.reload()` preserves active tab via hash
- Check all localization keys are alphabetically sorted in both `en.json` and `sl.json`

**Verify**: Full regression — every action that worked before still works. No dead code references. Build succeeds (`dotnet build`). Visual inspection at all breakpoints.

---

## Critical Files Reference

| File | Role |
|------|------|
| `Views/Dashboard/UserDashboard.cshtml` | Main orchestrator — rewritten in Step 1 |
| `Views/Dashboard/_MyRentals.cshtml` | Source of renter deposit/dispute logic — migrated in Step 3 |
| `Views/Dashboard/_RentingOut.cshtml` | Source of owner deposit/dispute logic — migrated in Step 4 |
| `Views/Dashboard/_DashboardSidebar.cshtml` | Items/favorites/earnings — distributed across tabs |
| `wwwroot/js/dashboard.js` | Tab switching, expand/collapse, existing extension/review/SignalR code |
| `wwwroot/js/dashboard-disputes.js` | Deposit/dispute modal JS — NO changes |
| `Models/ViewModels/DashboardViewModel.cs` | ViewModel contract — NO changes |
| `Controllers/Mvc/DashboardController.cs` | Backend — NO changes |
| `Views/Shared/_StatusBadge.cshtml` | Reuse for status badges |
| `Views/Shared/_CountdownTimer.cshtml` | Reuse for deadline countdowns |
| `Views/Shared/_EmptyState.cshtml` | Reuse for empty states |
| `Views/Shared/_StatCard.cshtml` | Reuse for stats (fix dark mode in Step 2) |

## Risk Areas

1. **Deposit/dispute migration (Steps 3-4)**: Most complex. The `_MyRentals.cshtml` and `_RentingOut.cshtml` have deeply nested conditionals with `onclick` handlers passing serialized evidence JSON. Must preserve identical handler signatures.
2. **SignalR tab persistence**: Current handlers call `location.reload()`. Hash-based tab persistence (Step 1) handles this automatically.
3. **`_StatCard` dark mode fix (Step 2)**: Additive change (`dark:` classes) — won't break light mode or other pages using this partial.

## Verification (End-to-End)

After all steps:
1. `dotnet build RentMate.sln` — succeeds
2. `dotnet run --project RentMate-Web/RentMate.csproj` — starts without errors
3. Navigate to `/Dashboard` — Home tab loads with stats + attention + quick links
4. Switch to each tab — content renders, hash updates
5. Test expand/collapse on rental rows in Renting + Lending tabs
6. Test ALL modal flows: extend rental, deposit resolution, early return, dispute, counter-offer, maintain charge, add evidence, review, escalation
7. Test approve/decline rental requests and extensions
8. Test toggle listing, edit item, delete item in Lending tab
9. Test history filters and load more
10. Test dark mode across all tabs
11. Test mobile (375px) responsiveness
12. Test SignalR: trigger a notification, verify page reloads to correct tab

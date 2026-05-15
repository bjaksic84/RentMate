# Onboarding Redesign - Design Spec

## Context

The current onboarding is a 2-step form (name+city, then optional photo) with no welcome screen, no intent collection, no app tour, and no localization. It's functional but forgettable. This redesign creates a modern onboarding experience that collects user intent, teaches the app, and personalizes the first experience.

## Design Decisions

| Decision | Choice | Reasoning |
|----------|--------|-----------|
| Philosophy | Hybrid (essentials + intent + activation) | Balances data collection with speed |
| Intent collection | Ask upfront (rent / list / both) | Enables personalized tour + CTA |
| Step count | 4 wizard steps + spotlight tour | Balanced: enough data, low drop-off |
| Visual tone | Clean & minimal | Matches existing RentMate aesthetic |
| Location | Optional with toggle | Privacy-conscious, not everyone wants to share |
| Tour for "Both" intent | Full renter carousel, then full lister carousel | Both sides need complete coverage |
| Spotlight tour | Intent-adaptive, 4 stops | Teaches navigation on the real UI |
| Illustrations | Placeholder system with text briefs, desktop + mobile variants | Unblocks implementation |
| Layout | Dedicated onboarding shell (no navbar/footer) | Focus and immersion |

## Flow Overview

```
Registration
  |
  v
Step 1: Welcome + Intent  (rent / list / both)
  |
  v
Step 2: Name + Location   (location optional via toggle)
  |
  v
Step 3: Photo + Bio       (both optional, skippable)
  |
  v
Step 4: App Tour Carousel  (content adapts to intent)
  |  - Renter: 4 slides + CTA
  |  - Lister: 4 slides + CTA
  |  - Both:   4 renter slides + transition + 4 lister slides + CTA
  |
  v
Homepage with Spotlight Tour  (4 stops, intent-adaptive)
```

## Onboarding Layout Shell

Steps 1-4 render inside a **dedicated `_OnboardingLayout.cshtml`** with no navbar, no footer, and no sidebar. This removes all distractions and creates an immersive wizard experience.

**Layout contains:**
- Small RentMate logo (top-left corner, links to nothing during onboarding)
- "Log out" text link (top-right corner, for users who need to bail)
- Centered content area (max-width ~560px desktop, full-width mobile with padding)
- Clean background: `var(--bg)` with a subtle radial gradient wash for depth (light: faint blue-to-transparent from center, dark: faint slate glow)

Step 5 (spotlight tour) uses the full app layout since it operates on the real UI.

## Step Transition Choreography

Every step transition follows a consistent entrance/exit choreography. This is the single biggest differentiator between "form wizard" and "major platform" onboarding.

**Exit animation (current step):**
- All elements fade out simultaneously (opacity 1 to 0, ~150ms ease-out)
- Slight slide to the left (translateX 0 to -20px)

**Enter animation (new step):**
- Elements enter **staggered**, not all at once:
  1. Heading fades in + slides up (delay: 0ms)
  2. Subtext fades in + slides up (delay: 50ms)
  3. Form fields / cards fade in + slides up (delay: 100ms)
  4. Buttons fade in + slides up (delay: 150ms)
- Each element: opacity 0 to 1, translateY 12px to 0, ~300ms ease-out
- Stagger creates a gentle cascade that feels alive

**Progress dot animation:**
- Dots animate their fill with a smooth 300ms color transition (not instant snaps)
- Newly active dot scales up briefly (1.0 to 1.3 to 1.0) as it fills

**Back navigation:**
- Reverses the direction: exit slides right, enter slides from left
- Same stagger timing

## Step Guards & Routing

Each step validates that prerequisites are met before rendering. This prevents users from skipping ahead by typing URLs directly.

| Step | Guard | Redirect if failed |
|------|-------|--------------------|
| Step 1 | `OnboardingCompleted == true` | Redirect to Home |
| Step 2 | `UserIntent == null` | Redirect to Step 1 |
| Step 2 | `OnboardingCompleted == true` | Redirect to Home |
| Step 3 | `FirstName` is empty | Redirect to Step 1 |
| Step 3 | `OnboardingCompleted == true` | Redirect to Home |
| Step 4 | `FirstName` is empty | Redirect to Step 1 |
| Step 4 | `OnboardingCompleted == true` | Redirect to Home |

**`OnboardingCompleted` is set to `true`** when the user clicks the CTA on the final carousel slide (Step 4). This is the single point where onboarding is marked complete. The carousel "Skip tour" also triggers this (skipping jumps to the final CTA slide, clicking the CTA completes it).

**Existing user migration:** Users who already completed the old onboarding are left alone. They keep `OnboardingCompleted = true`, `UserIntent = null`, `SpotlightTourCompleted = false`. They will not see the new wizard or spotlight tour. The spotlight tour only fires when `OnboardingCompleted` was just set to `true` in the current session (tracked via `TempData["ShowSpotlightTour"]`), not for legacy users.

**Page refresh behavior:** Each step is a separate MVC route. Refreshing reloads the current step from the server. On Step 3, if a photo was previewed but not yet submitted, the preview is lost (FileReader is in-memory only). The upload area resets to its empty state. Bio text typed but not yet submitted is also lost since Step 3 hasn't been POSTed yet. This is acceptable and expected browser behavior.

## Step 1: Welcome + Intent

**Purpose:** First impression + collect user intent for personalization.

**Layout:**
- Progress dots (4 dots, first active)
- Hero illustration placeholder (centered)
- "Welcome to RentMate" heading (Outfit, 700 weight)
- Value proposition: "Rent anything from people around you, or earn by sharing what you own."
- "What brings you here?" subheading
- 3 intent cards

**Intent Cards:**
| Card | Icon | Label | Description | Background |
|------|------|-------|-------------|------------|
| Rent | `bi-search` | I want to rent | Find and rent items from people near you | Subtle blue wash (`blue-50` / `dark:blue-950/30`) |
| List | `bi-box-seam` | I want to list | Earn money by sharing your stuff | Subtle green wash (`emerald-50` / `dark:emerald-950/30`) |
| Both | `bi-arrow-left-right` | Both | Rent and list, best of both worlds | Subtle amber wash (`amber-50` / `dark:amber-950/30`) |

**Card interaction states:**
- **Default:** Soft colored background, 1px border matching the wash, rounded-2xl
- **Hover:** Lift up (translateY -2px), shadow increases from `shadow-sm` to `shadow-md`, border color intensifies. 200ms ease transition.
- **Click/Selection:** Brief press effect (scale 0.98 for 100ms, then scale 1.02 for 200ms), border turns solid blue, checkmark icon appears in top-right corner. After 400ms delay, auto-advances to Step 2.

**Behavior:**
- Desktop: 3 cards side by side. Mobile: stacked vertically with horizontal layout per card
- No submit button, no back button (first step)
- Intent saved to `ApplicationUser.UserIntent` (new enum field)

**Data model change:** Add `UserIntent` enum (Renter, Lister, Both) and field to `ApplicationUser`.

## Step 2: Name + Location

**Purpose:** Collect identity and optional location for proximity features.

**Layout:**
- Progress dots (dots 1-2 active)
- "Tell us about yourself" heading
- "This helps build trust with other members." subtext
- First name + Last name fields (side by side on desktop, stacked on mobile)
- Divider
- Location section with header, optional badge, and toggle

**Form field interactions (text inputs only, not dropdowns):**
- **Floating labels:** Labels start as placeholder text inside the field. On focus (or when field has value), label animates up to a small position above the field border (translateY, font-size reduction from 0.9em to 0.75em, 150ms ease). Color shifts from muted to primary blue on focus.
- **Border transition:** Field border transitions from `var(--border)` to `var(--primary)` on focus (150ms ease, not instant).
- **Validation:** Invalid fields get a red border + subtle shake animation (translateX -4px to 4px, 2 cycles, 200ms).
- **Dropdowns (Country/State/City):** Use standard labels above the select element (not floating). Floating labels on selects are unreliable since they always have a value.

**Location Section:**
- "Share location" toggle (defaults ON)
- Info box: "Pick the area closest to you. This is used to recommend items nearby and show your approximate area to other users. Your exact address is never shared."
- Country dropdown (defaults to Slovenia for now)
- State/Region dropdown
- City dropdown (uses current CityData as temporary source)

**Toggle OFF state:**
- Location fields collapse with height animation (300ms ease, overflow hidden)
- Replacement text: "Without a location, you'll still be able to browse and use RentMate, but items won't be sorted by distance and your profile won't show an area. You can always add this later in settings."

**Location system note:** Country/State/City dropdowns are placeholders for a future location revamp. Currently only Slovenia + CityData cities are supported. The UI structure is ready for expansion.

**Behavior:**
- Continue button disabled until first name and last name are filled (location is optional)
- Back button returns to Step 1 (intent is preserved)

## Step 3: Photo + Bio

**Purpose:** Encourage profile photo upload and bio for trust building. Both optional.

**Layout:**
- Progress dots (dots 1-3 active)
- "Add a profile photo" heading
- "People are more likely to rent from someone they can see." subtext
- Circular upload area (180px desktop, 140px mobile) with dashed border
  - Desktop text: "Drag & drop" (primary) + "or click to browse" (secondary)
  - Mobile text: "Tap to upload"
- Social proof: "Profiles with photos get 3x more responses"
- File constraints: JPG, PNG, or WebP. Max 5MB.
- Divider
- "About you" textarea with "Optional" badge, character counter (500 max)
- Placeholder: "Tell others a bit about yourself..."

**Upload States:**
- Default: dashed circle, camera icon, drag/click text
- Drag hover (desktop): circle scales up slightly (1.05), border turns blue, background turns light blue, text becomes "Drop it here!"
- Uploaded: heading changes to "Looking good!", blue border, preview image, green checkmark, "Change photo" link

**Bio textarea interactions:**
- Same floating label pattern as Step 2 fields
- Character counter updates in real-time, turns amber at 400+, red at 480+

**Behavior:**
- Drag-and-drop on desktop, file picker on all platforms
- Instant preview via FileReader, actual upload on Continue click
- 5MB client-side validation with error toast
- "Skip for now" next to Continue button (both advance to Step 4)
- Back button returns to Step 2

## Step 4: App Tour Carousel

**Purpose:** Teach core concepts before dropping users into the app. Content adapts to intent.

**Layout:**
- All 4 onboarding progress dots active
- Card container with illustration placeholder + title + description
- Carousel dot indicators (pill shape for active, circle for inactive)
- "Skip tour" link + "Next" button

**Illustration placeholder design:**
Until real images are provided, each slide's illustration area uses:
- Themed gradient background (matching the slide's color: blue for renter, green for lister)
- Large faint slide number watermark ("01", "02", etc.) in the background at ~5% opacity, Outfit font, 6em size
- Subtle concentric circle pattern (3 circles, 2% opacity, centered) for visual texture
- This ensures slides feel designed even without final artwork

### Renter Slides (4)

| # | Title | Description | Illustration key |
|---|-------|-------------|-----------------|
| 1 | Find what you need | Search by category, location, or keyword. Filter by price, distance, and availability to find exactly what you're looking for. | tour-renter-browse |
| 2 | Rent with confidence | Pick your dates, review the price breakdown, and send a rental request. The owner confirms and you're all set. | tour-renter-rent |
| 3 | Secure deposits | Deposits protect both sides. They're held safely and released automatically when the rental ends without issues. | tour-renter-deposit |
| 4 | Reviews build trust | After each rental, both sides leave a review. Verified reviews help the community stay trustworthy. | tour-renter-reviews |

### Lister Slides (4)

| # | Title | Description | Illustration key |
|---|-------|-------------|-----------------|
| 1 | Create a listing | Add photos, set your price, and describe your item. It only takes a few minutes to go live. | tour-lister-create |
| 2 | Manage requests | Review rental requests from your dashboard. Accept, decline, or suggest different dates. | tour-lister-manage |
| 3 | Earn money | Get paid for each rental. Track your earnings and payouts from your dashboard. | tour-lister-earn |
| 4 | Build your reputation | Great reviews attract more renters. Respond quickly and keep items in good shape to grow your trust score. | tour-lister-reviews |

### "Both" Intent Flow (9 slides)

Renter slide 1 > 2 > 3 > 4 > **Transition slide** > Lister slide 1 > 2 > 3 > 4 > **Final CTA**

**Transition slide:**
- Animated green checkmark (SVG stroke-dashoffset draw animation, ~600ms)
- "That's how renting works!"
- "Now let's look at the other side: listing your items and earning money."
- "Show me" button

**Section labels (Both intent only):**
- Renter slides: blue "RENTING ITEMS" label
- Lister slides: green "LISTING YOUR ITEMS" label

**Final slide (all intents):**
- Animated checkmark that draws itself (SVG stroke-dashoffset, ~600ms)
- Personalized heading: "You're all set, {FirstName}!" (uses name from Step 2)
- Social proof stat: "Join {memberCount} other members in {City}" (if location shared) or "Join {memberCount} other members on RentMate" (if no location). Member count = total registered users from `_db.Users.Count()`, passed to the view model by the controller.
- Intent-specific CTA button:
  - Renter: "Browse items near you"
  - Lister: "List your first item"
  - Both: "Explore RentMate"

**Behavior:**
- Swipe gestures on mobile, click/arrow keys on desktop
- Left/Right arrow keys navigate slides, Enter triggers CTA on final slide
- Tab focuses "Skip tour" then "Next" button in order
- Smooth horizontal slide transition (~300ms)
- "Skip tour" jumps to final CTA slide
- Carousel dots: completed dots dim, active dot is elongated pill, transition dot is amber

## Step 5: Spotlight Tour (on the real app)

**Purpose:** Orient users in the actual UI by highlighting key elements with tooltips.

**Trigger:** Fires on first homepage load after onboarding completion. Detected via `TempData["ShowSpotlightTour"]` set by the Step 4 CTA action, not by checking `SpotlightTourCompleted == false` (which would catch legacy users).

**Overlay mechanics:**
- Dark semi-transparent overlay dims everything except the highlighted element
- Highlighted elements are targeted via `data-spotlight="search"`, `data-spotlight="item-card"`, etc. attributes added to the relevant elements in the main layout. This decouples the spotlight JS from fragile DOM structure.
- Highlighted element: blue border + **animated sonar ring** (a pulsing ring that expands outward from the element border and fades out, repeating every 2s). Created with a pseudo-element using scale + opacity animation. Draws the eye much better than a static glow.
- Desktop: tooltip with arrow pointer appears near the element
- Mobile: bottom sheet slides up from bottom for thumb accessibility

**Tooltip entrance animation:**
- Tooltip slides in from the direction of the highlighted element (if element is at top, tooltip slides down from above; if element is at left, tooltip slides in from left)
- Combined with fade (opacity 0 to 1), ~250ms ease-out

**Tooltip content:**
- Step counter badge ("Step 1 of 4")
- Title (bold, Outfit font)
- 1-line description
- "Skip tour" link + "Next" button

### Renter Stops (4)

| # | Element | Title | Description |
|---|---------|-------|-------------|
| 1 | Search bar | Search for anything | Type a keyword, category, or item name to find what you need. Results are sorted by distance from your location. |
| 2 | Item card | Browse listings | Each item shows the price, location, rating, and availability. Click to see the full details. |
| 3 | Dashboard link | Your dashboard | Your dashboard is where you manage all your rentals, track requests, and handle deposits. |
| 4 | Notification bell | Stay updated | You'll get notified here when someone responds to your rental request or when there's activity on your account. |

### Lister Stops (4)

| # | Element | Title | Description |
|---|---------|-------|-------------|
| 1 | "List Item" button | Create a listing | Start here to create your first listing. Add photos, set a price, and go live in minutes. |
| 2 | Dashboard link | Your dashboard | Your dashboard shows incoming rental requests, active rentals, and your earnings overview. |
| 3 | Notification bell | Stay updated | You'll get notified here when someone wants to rent your items or leaves a review. |
| 4 | Profile menu | Your account | Access your profile, settings, and account options here. You can update your photo and bio anytime. |

### "Both" Stops (4)

Combined: Search bar, "List Item" button, Dashboard link, Notification bell.

**Final stop celebration:**
When the user clicks "Next" on the last spotlight stop, a brief sparkle/confetti burst animation plays before the overlay dissolves. Small detail, big emotional payoff. CSS-only using multiple small circles/stars that animate outward from center with randomized delays and directions, then the overlay fades out over 400ms.

**Dismissal:**
- "Skip tour" link on any tooltip
- Clicking outside the tooltip
- Pressing Escape
- Completing all 4 stops

**Persistence:**
- `localStorage` key: `rentmate_spotlight_completed` (immediate, no network dependency)
- `ApplicationUser.SpotlightTourCompleted` boolean (synced to DB, persists across devices)
- Tour never shows again once dismissed or completed

## Data Model Changes

### ApplicationUser additions

```csharp
// Onboarding intent
public UserIntent? UserIntent { get; set; }  // null until Step 1

// Spotlight tour
public bool SpotlightTourCompleted { get; set; }  // default: false

// Bio (already exists, max 500 chars)
// OnboardingCompleted (already exists)
// FirstName, LastName, City, ProfilePictureUrl (already exist)
```

### New enum

```csharp
public enum UserIntent
{
    Renter,
    Lister,
    Both
}
```

### Migration

- Add `UserIntent` (nullable string, enum-to-string conversion like other enums)
- Add `SpotlightTourCompleted` (boolean, default false)

## Image Asset System

All tour illustrations are stored as static placeholders in `wwwroot/images/onboarding/`.

Each illustration has a text file brief (what to depict) and two size variants: desktop and mobile.

### Image briefs to create

**Step 1:**
- `welcome-hero-desktop.txt` / `welcome-hero-mobile.txt`

**Renter carousel (Step 4):**
- `tour-renter-browse-desktop.txt` / `tour-renter-browse-mobile.txt`
- `tour-renter-rent-desktop.txt` / `tour-renter-rent-mobile.txt`
- `tour-renter-deposit-desktop.txt` / `tour-renter-deposit-mobile.txt`
- `tour-renter-reviews-desktop.txt` / `tour-renter-reviews-mobile.txt`

**Lister carousel (Step 4):**
- `tour-lister-create-desktop.txt` / `tour-lister-create-mobile.txt`
- `tour-lister-manage-desktop.txt` / `tour-lister-manage-mobile.txt`
- `tour-lister-earn-desktop.txt` / `tour-lister-earn-mobile.txt`
- `tour-lister-reviews-desktop.txt` / `tour-lister-reviews-mobile.txt`

**Total: 18 text files (9 illustrations x 2 sizes)**

Actual images will replace these briefs. Placeholder gradient boxes with decorative elements (watermark numbers, concentric circles) used in code until real images are provided.

## Cross-Cutting Concerns

### Progress Indicator
- Minimal step dots (not numbered), shows position without pressure
- Active dots filled blue, completed dots filled blue, upcoming dots gray
- Dot transitions are animated (300ms fill, brief scale pulse on activation)
- 4 dots total (Steps 1-4). Spotlight tour has its own step counter inside tooltips.

### Animations
- **Step transitions:** Staggered enter (heading > subtext > content > buttons, 50ms intervals), simultaneous fade-out on exit. See "Step Transition Choreography" section for full spec.
- **Intent card selection:** hover lift (translateY -2px), click press (scale 0.98 > 1.02), 400ms delay before advance
- **Form field focus:** Floating label animation (150ms), border color transition (150ms)
- **Photo drag hover:** scale(1.05) + blue border + blue background
- **Carousel slides:** Horizontal slide transition, ~300ms
- **Spotlight sonar ring:** Expanding ring around highlighted element, repeating every 2s
- **Spotlight tooltip:** Directional slide-in from highlighted element, ~250ms
- **Completion celebrations:** SVG checkmark draw animation (600ms), confetti burst on spotlight finish
- **Location toggle:** Collapse/expand with height animation (300ms)
- **Validation errors:** Horizontal shake (translateX -4px to 4px, 2 cycles, 200ms)

### Navigation
- Back button on Steps 2, 3 (not Step 1 or carousel)
- "Skip for now" on Step 3 (photo/bio)
- "Skip tour" on Step 4 (carousel) and Step 5 (spotlight)
- Each step is a separate MVC action/route, so browser back works naturally

### Keyboard Navigation (Carousel + Spotlight)
- **Carousel:** Left/Right arrow keys navigate slides, Enter triggers CTA on final slide, Tab cycles through "Skip tour" then "Next"
- **Spotlight:** Tab focuses tooltip buttons, Escape dismisses, Enter triggers focused button

### Localization
- All user-visible strings via `@Localizer["KeyName"]` in Razor views
- Client-side strings via `window.T.KeyName` proxy
- Keys added to both `Resources/en.json` and `Resources/sl.json`, alphabetically sorted

### Dark Mode
- Full support from day one using existing `dark:` Tailwind prefix
- All colors reference CSS variables or Tailwind dark variants
- Illustration placeholders use CSS gradients that adapt to theme
- Intent card wash backgrounds adapt (e.g., `blue-50` light, `blue-950/30` dark)

### Responsive Design
- Mobile-first with Tailwind breakpoints
- Step 1 intent cards: side by side (desktop) vs stacked (mobile)
- Step 2 name fields: side by side (desktop) vs stacked (mobile)
- Step 3 photo circle: 180px (desktop) vs 140px (mobile)
- Step 4 carousel: full width on mobile, constrained on desktop
- Step 5 spotlight: floating tooltip (desktop) vs bottom sheet (mobile)

### Reduced Motion
- All animations wrapped in `@media (prefers-reduced-motion: no-preference)` checks
- When `prefers-reduced-motion: reduce` is set: step transitions are instant (no slide/fade), carousel slides swap instantly, spotlight appears without sonar ring or directional slide, confetti is skipped, SVG checkmark appears without draw animation
- Functional behavior remains identical, only decorative motion is removed

### Accessibility
- Focus states on all interactive elements (blue ring, 2px offset)
- ARIA labels on progress indicators, buttons, form fields
- Keyboard navigation: Tab through fields, Enter to submit, Escape to dismiss spotlight
- Screen reader: progress dots announced as "Step X of 4"
- Sufficient color contrast in both light and dark modes
- Floating labels maintain accessibility (label element always present, visually repositioned)

## Files to Modify

### Existing files
- `Controllers/Mvc/OnboardingController.cs` (rewrite: 4 steps + intent + optional location)
- `Models/Domain/ApplicationUser.cs` (add UserIntent, SpotlightTourCompleted)
- `Views/Onboarding/Step1.cshtml` (rewrite: welcome + intent)
- `Views/Onboarding/Step2.cshtml` (rewrite: name + location with toggle)
- `Areas/Identity/Pages/Account/Register.cshtml.cs` (redirect still points to Step1)
- `Program.cs` (register any new services if needed)
- `Resources/en.json` and `Resources/sl.json` (new localization keys)
- `Infrastructure/Data/RentMateContext.cs` (enum conversion for UserIntent)
- `Views/Shared/_NavBar.cshtml` (add `data-spotlight` attributes to search bar, dashboard link, notification bell, list item button, profile menu)
- `Views/Shared/_Layout.cshtml` (reference `onboarding.css`; spotlight tour JS init from TempData flag)

### New files
- `Views/Shared/_OnboardingLayout.cshtml` (dedicated onboarding shell, no navbar/footer)
- `Views/Onboarding/Step3.cshtml` (photo + bio)
- `Views/Onboarding/Step4.cshtml` (carousel tour)
- `wwwroot/js/onboarding.js` (carousel, drag-drop, animations, spotlight)
- `wwwroot/css/onboarding.css` (step transitions, floating labels, sonar ring, confetti, SVG draw animations)
- `wwwroot/images/onboarding/*.txt` (18 image brief files)
- New EF migration for UserIntent + SpotlightTourCompleted
- `Models/Domain/UserIntent.cs` (enum)

### Files to remove
- Current `OnboardingStep1ViewModel` and `OnboardingStep2ViewModel` (inline in controller, will be replaced with proper ViewModels)

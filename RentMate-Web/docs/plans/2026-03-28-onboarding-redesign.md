# Onboarding Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the basic 2-step onboarding with a polished 4-step wizard (welcome+intent, name+location, photo+bio, carousel tour) plus a spotlight tour on the homepage, with intent-adaptive content, animations, and full dark mode support.

**Architecture:** New `UserIntent` enum and `SpotlightTourCompleted` field on `ApplicationUser`. Rewritten `OnboardingController` with 4 GET/POST pairs plus a completion action. Dedicated `_OnboardingLayout.cshtml` shell (no navbar/footer). Four Razor views with staggered CSS animations. Single `onboarding.js` for carousel, drag-drop, floating labels, and spotlight tour. Single `onboarding.css` for all onboarding animations. Intent-adaptive carousel slides and spotlight stops. TempData-triggered spotlight tour on first homepage load after onboarding.

**Tech Stack:** ASP.NET Core MVC (net10.0), EF Core + PostgreSQL, Tailwind CSS (CDN), vanilla JS, Bootstrap Icons, SignalR (existing)

**Design Spec:** `docs/superpowers/specs/2026-03-28-onboarding-redesign-design.md`

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `Models/Domain/UserIntent.cs` | Enum: Renter, Lister, Both |
| `Models/ViewModels/OnboardingViewModels.cs` | View models for all 4 steps |
| `Views/Shared/_OnboardingLayout.cshtml` | Dedicated onboarding layout shell (no navbar/footer) |
| `Views/Onboarding/Step3.cshtml` | Photo + Bio step |
| `Views/Onboarding/Step4.cshtml` | Carousel tour step |
| `wwwroot/css/onboarding.css` | All onboarding animations: step transitions, floating labels, sonar ring, confetti, SVG draw |
| `wwwroot/js/onboarding.js` | All client-side logic: carousel, drag-drop, floating labels, spotlight tour, animations |
| `wwwroot/images/onboarding/*.txt` | 18 image brief placeholders (9 illustrations x 2 sizes) |
| EF Migration (auto-generated) | Adds UserIntent + SpotlightTourCompleted columns |

### Modified Files

| File | Change |
|------|--------|
| `Models/Domain/ApplicationUser.cs` | Add `UserIntent?` and `SpotlightTourCompleted` fields |
| `Infrastructure/Data/RentMateContext.cs` | Add UserIntent enum-to-string conversion |
| `Controllers/Mvc/OnboardingController.cs` | Full rewrite: 4 steps, guards, completion action, new view models |
| `Views/Onboarding/Step1.cshtml` | Full rewrite: welcome + intent cards |
| `Views/Onboarding/Step2.cshtml` | Full rewrite: name + location with toggle |
| `Views/Shared/_NavBar.cshtml` | Add `data-spotlight` attributes to key elements |
| `Views/Shared/_Layout.cshtml` | Add onboarding.css link, conditional spotlight JS init |
| `Resources/en.json` | ~50 new localization keys |
| `Resources/sl.json` | ~50 new localization keys |

---

## Task 1: Data Model + Migration

**Files:**
- Create: `Models/Domain/UserIntent.cs`
- Modify: `Models/Domain/ApplicationUser.cs:51-55`
- Modify: `Infrastructure/Data/RentMateContext.cs:64-102`

- [ ] **Step 1: Create `UserIntent.cs`**

```csharp
namespace RentMate.Models.Domain;

/// <summary>
/// What the user primarily wants to do on RentMate.
/// Collected during onboarding Step 1 to personalize the experience.
/// </summary>
public enum UserIntent
{
    Renter,
    Lister,
    Both
}
```

- [ ] **Step 2: Add fields to `ApplicationUser.cs`**

Add these two fields in the `// -- Onboarding --` region, after the existing `OnboardingCompleted` property (line 55):

```csharp
/// <summary>
/// User's primary intent: rent, list, or both. Set during onboarding Step 1.
/// Null for legacy users who completed the old onboarding.
/// </summary>
public UserIntent? UserIntent { get; set; }

/// <summary>
/// Whether the user has completed (or dismissed) the post-onboarding spotlight tour.
/// </summary>
public bool SpotlightTourCompleted { get; set; }
```

- [ ] **Step 3: Add enum conversion in `RentMateContext.cs`**

In the `ConfigureUserRelationships` method, after the existing `DeactivatedBy` conversion (line 101), add:

```csharp
// Store UserIntent as string for readability
modelBuilder.Entity<ApplicationUser>()
    .Property(u => u.UserIntent)
    .HasConversion<string>();
```

- [ ] **Step 4: Create EF migration**

Run:
```bash
dotnet ef migrations add AddUserIntentAndSpotlightTour --project RentMate-Web/RentMate.csproj
```

Expected: Migration files created in `Infrastructure/Data/Migrations/`.

- [ ] **Step 5: Apply migration**

Run:
```bash
dotnet ef database update --project RentMate-Web/RentMate.csproj
```

Expected: Database updated successfully. Two new columns: `UserIntent` (text, nullable) and `SpotlightTourCompleted` (boolean, default false).

- [ ] **Step 6: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeded, 0 errors.

---

## Task 2: View Models

**Files:**
- Create: `Models/ViewModels/OnboardingViewModels.cs`

- [ ] **Step 1: Create `OnboardingViewModels.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using RentMate.Models.Domain;

namespace RentMate.Models.ViewModels;

/// <summary>
/// Step 1: Welcome + Intent selection.
/// </summary>
public class OnboardingStep1ViewModel
{
    /// <summary>Selected intent (posted via hidden field when a card is clicked).</summary>
    [Required]
    public UserIntent? SelectedIntent { get; set; }
}

/// <summary>
/// Step 2: Name + optional Location.
/// </summary>
public class OnboardingStep2ViewModel
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    /// <summary>Whether the user wants to share location. Defaults to true.</summary>
    public bool ShareLocation { get; set; } = true;

    [Display(Name = "Country")]
    public string? Country { get; set; }

    [Display(Name = "State / Region")]
    public string? State { get; set; }

    [Display(Name = "City")]
    public string? City { get; set; }

    /// <summary>Populated by controller for the city dropdown.</summary>
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> CityOptions { get; set; } = new();
}

/// <summary>
/// Step 3: Photo + Bio (both optional).
/// </summary>
public class OnboardingStep3ViewModel
{
    public string? ExistingProfilePictureUrl { get; set; }

    [Display(Name = "Profile Picture")]
    public IFormFile? ProfilePicture { get; set; }

    [StringLength(500)]
    [Display(Name = "About you")]
    public string? Bio { get; set; }
}

/// <summary>
/// Step 4: Carousel tour. Read-only data for the view.
/// </summary>
public class OnboardingStep4ViewModel
{
    public UserIntent UserIntent { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? City { get; set; }
    public bool ShareLocation { get; set; }
    public int MemberCount { get; set; }
}
```

- [ ] **Step 2: Remove old view models from controller file**

In `Controllers/Mvc/OnboardingController.cs`, delete lines 176-201 (the old `OnboardingStep1ViewModel` and `OnboardingStep2ViewModel` classes at the bottom of the file). These will be replaced by the new file.

- [ ] **Step 3: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build errors in OnboardingController because it still references the old view model types. This is expected and will be fixed in Task 5.

---

## Task 3: Onboarding CSS

**Files:**
- Create: `wwwroot/css/onboarding.css`

- [ ] **Step 1: Create `onboarding.css`**

```css
/* ═══════════════════════════════════════════════════════════════
   Onboarding CSS
   Step transitions, floating labels, sonar ring, confetti,
   SVG draw animations, carousel, spotlight tour
   ═══════════════════════════════════════════════════════════════ */

/* ── Step Transition Choreography ─────────────────────────────── */

.onboarding-step {
    position: relative;
}

/* Staggered entrance: each child gets a delay class */
.step-enter {
    opacity: 0;
    transform: translateY(12px);
}

.step-enter.step-enter-active {
    opacity: 1;
    transform: translateY(0);
    transition: opacity 300ms ease-out, transform 300ms ease-out;
}

.step-enter-delay-0 { transition-delay: 0ms; }
.step-enter-delay-1 { transition-delay: 50ms; }
.step-enter-delay-2 { transition-delay: 100ms; }
.step-enter-delay-3 { transition-delay: 150ms; }

/* Exit animation (simultaneous, no stagger) */
.step-exit {
    opacity: 1;
    transform: translateX(0);
    transition: opacity 150ms ease-out, transform 150ms ease-out;
}

.step-exit-active {
    opacity: 0;
    transform: translateX(-20px);
}

/* Back navigation reversal */
.step-exit-back.step-exit-active {
    transform: translateX(20px);
}

.step-enter-back {
    opacity: 0;
    transform: translateY(12px);
}

.step-enter-back.step-enter-active {
    opacity: 1;
    transform: translateY(0);
    transition: opacity 300ms ease-out, transform 300ms ease-out;
}

/* ── Progress Dots ────────────────────────────────────────────── */

.progress-dot {
    width: 8px;
    height: 8px;
    border-radius: 9999px;
    background-color: #cbd5e1; /* slate-300 */
    transition: background-color 300ms ease, transform 300ms ease;
}

.dark .progress-dot {
    background-color: #475569; /* slate-600 */
}

.progress-dot.active {
    background-color: #2563eb; /* blue-600 */
}

.progress-dot.completed {
    background-color: #2563eb;
}

@keyframes dotPulse {
    0% { transform: scale(1); }
    50% { transform: scale(1.3); }
    100% { transform: scale(1); }
}

.progress-dot.pulse {
    animation: dotPulse 300ms ease;
}

/* ── Floating Labels ──────────────────────────────────────────── */

.floating-field {
    position: relative;
}

.floating-field input {
    padding-top: 1.25rem;
}

.floating-field label {
    position: absolute;
    left: 0.75rem;
    top: 50%;
    transform: translateY(-50%);
    font-size: 0.9em;
    color: #6c757d;
    pointer-events: none;
    transition: all 150ms ease;
    transform-origin: left center;
}

.dark .floating-field label {
    color: #94a3b8; /* slate-400 */
}

.floating-field input:focus ~ label,
.floating-field input:not(:placeholder-shown) ~ label {
    top: 0.35rem;
    transform: translateY(0);
    font-size: 0.75em;
    color: #2563eb;
}

.dark .floating-field input:focus ~ label,
.dark .floating-field input:not(:placeholder-shown) ~ label {
    color: #60a5fa; /* blue-400 */
}

.floating-field input {
    transition: border-color 150ms ease;
}

.floating-field input:focus {
    border-color: #2563eb;
}

/* ── Intent Cards ─────────────────────────────────────────────── */

.intent-card {
    cursor: pointer;
    transition: transform 200ms ease, box-shadow 200ms ease, border-color 200ms ease;
    user-select: none;
}

.intent-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 6px -1px rgba(0,0,0,.1), 0 2px 4px -2px rgba(0,0,0,.1);
}

@keyframes intentPress {
    0% { transform: scale(1); }
    30% { transform: scale(0.98); }
    100% { transform: scale(1.02); }
}

.intent-card.selected {
    animation: intentPress 300ms ease forwards;
    border-color: #2563eb !important;
}

.intent-card .intent-check {
    opacity: 0;
    transform: scale(0);
    transition: all 200ms ease;
}

.intent-card.selected .intent-check {
    opacity: 1;
    transform: scale(1);
}

/* ── Validation Shake ─────────────────────────────────────────── */

@keyframes shake {
    0%, 100% { transform: translateX(0); }
    25% { transform: translateX(-4px); }
    75% { transform: translateX(4px); }
}

.field-error {
    animation: shake 200ms ease;
    animation-iteration-count: 2;
}

/* ── Photo Upload ─────────────────────────────────────────────── */

.photo-upload-area {
    transition: transform 200ms ease, border-color 200ms ease, background-color 200ms ease;
}

.photo-upload-area.drag-over {
    transform: scale(1.05);
    border-color: #2563eb;
    background-color: #eff6ff;
}

.dark .photo-upload-area.drag-over {
    background-color: rgba(37, 99, 235, 0.1);
}

/* ── Location Toggle Collapse ─────────────────────────────────── */

.location-fields {
    overflow: hidden;
    transition: max-height 300ms ease, opacity 300ms ease;
    max-height: 500px;
    opacity: 1;
}

.location-fields.collapsed {
    max-height: 0;
    opacity: 0;
}

/* ── Carousel ─────────────────────────────────────────────────── */

.carousel-track {
    display: flex;
    transition: transform 300ms ease;
}

.carousel-slide {
    min-width: 100%;
    flex-shrink: 0;
}

.carousel-dot {
    width: 8px;
    height: 8px;
    border-radius: 9999px;
    background-color: #cbd5e1;
    transition: all 300ms ease;
}

.dark .carousel-dot {
    background-color: #475569;
}

.carousel-dot.active {
    width: 24px;
    background-color: #2563eb;
}

.carousel-dot.completed {
    background-color: #94a3b8;
}

.carousel-dot.transition-dot.active {
    background-color: #f59e0b; /* amber-500 */
}

/* ── SVG Checkmark Draw ───────────────────────────────────────── */

.checkmark-svg {
    stroke-dasharray: 100;
    stroke-dashoffset: 100;
}

.checkmark-svg.animate {
    animation: drawCheck 600ms ease forwards;
}

@keyframes drawCheck {
    to { stroke-dashoffset: 0; }
}

/* ── Spotlight Tour ───────────────────────────────────────────── */

.spotlight-overlay {
    position: fixed;
    inset: 0;
    z-index: var(--z-modal-backdrop, 1100);
    pointer-events: all;
}

.spotlight-overlay-bg {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.6);
    transition: opacity 400ms ease;
}

.spotlight-hole {
    position: absolute;
    border-radius: 12px;
    box-shadow: 0 0 0 9999px rgba(0, 0, 0, 0.6);
    border: 2px solid #2563eb;
    pointer-events: none;
    transition: all 300ms ease;
}

/* Sonar ring animation */
.spotlight-hole::before {
    content: '';
    position: absolute;
    inset: -6px;
    border-radius: inherit;
    border: 2px solid #2563eb;
    animation: sonarRing 2s ease-out infinite;
}

@keyframes sonarRing {
    0% {
        transform: scale(1);
        opacity: 0.8;
    }
    100% {
        transform: scale(1.3);
        opacity: 0;
    }
}

/* Desktop tooltip */
.spotlight-tooltip {
    position: absolute;
    z-index: var(--z-modal, 1200);
    background: white;
    border-radius: 1rem;
    padding: 1.25rem;
    box-shadow: 0 20px 25px -5px rgba(0,0,0,.1), 0 8px 10px -6px rgba(0,0,0,.1);
    max-width: 320px;
    width: max-content;
    opacity: 0;
    transition: opacity 250ms ease-out, transform 250ms ease-out;
}

.dark .spotlight-tooltip {
    background: #1e293b;
    border: 1px solid #334155;
}

.spotlight-tooltip.visible {
    opacity: 1;
    transform: translate(0, 0);
}

/* Tooltip arrow */
.spotlight-tooltip::before {
    content: '';
    position: absolute;
    width: 12px;
    height: 12px;
    background: inherit;
    transform: rotate(45deg);
}

.spotlight-tooltip.arrow-top::before { top: -6px; left: 1.5rem; }
.spotlight-tooltip.arrow-bottom::before { bottom: -6px; left: 1.5rem; }
.spotlight-tooltip.arrow-left::before { left: -6px; top: 1.5rem; }
.spotlight-tooltip.arrow-right::before { right: -6px; top: 1.5rem; }

/* Mobile bottom sheet */
.spotlight-sheet {
    position: fixed;
    left: 0;
    right: 0;
    bottom: 0;
    z-index: var(--z-modal, 1200);
    background: white;
    border-radius: 1.5rem 1.5rem 0 0;
    padding: 1.5rem;
    padding-bottom: calc(1.5rem + env(safe-area-inset-bottom));
    box-shadow: 0 -10px 25px rgba(0,0,0,.1);
    transform: translateY(100%);
    transition: transform 300ms ease-out;
}

.dark .spotlight-sheet {
    background: #1e293b;
    border-top: 1px solid #334155;
}

.spotlight-sheet.visible {
    transform: translateY(0);
}

/* ── Confetti Burst ───────────────────────────────────────────── */

.confetti-container {
    position: fixed;
    inset: 0;
    pointer-events: none;
    z-index: var(--z-toast, 1500);
    overflow: hidden;
}

.confetti-piece {
    position: absolute;
    width: 8px;
    height: 8px;
    border-radius: 2px;
    left: 50%;
    top: 40%;
    opacity: 0;
}

@keyframes confettiFall {
    0% {
        opacity: 1;
        transform: translate(var(--x-start), var(--y-start)) rotate(0deg) scale(1);
    }
    100% {
        opacity: 0;
        transform: translate(var(--x-end), var(--y-end)) rotate(var(--rotation)) scale(0.5);
    }
}

.confetti-piece.animate {
    animation: confettiFall var(--duration) ease-out forwards;
    animation-delay: var(--delay);
}

/* ── Onboarding Layout Background ─────────────────────────────── */

.onboarding-bg {
    background: var(--bg);
    position: relative;
}

.onboarding-bg::before {
    content: '';
    position: fixed;
    inset: 0;
    background: radial-gradient(ellipse at center, rgba(37, 99, 235, 0.04) 0%, transparent 70%);
    pointer-events: none;
}

.dark .onboarding-bg::before {
    background: radial-gradient(ellipse at center, rgba(30, 41, 59, 0.5) 0%, transparent 70%);
}

/* ── Carousel Placeholder Illustrations ───────────────────────── */

.slide-illustration {
    position: relative;
    width: 100%;
    height: 200px;
    border-radius: 1rem;
    overflow: hidden;
    display: flex;
    align-items: center;
    justify-content: center;
}

.slide-illustration .watermark {
    position: absolute;
    font-family: 'Outfit', sans-serif;
    font-size: 6em;
    font-weight: 800;
    opacity: 0.05;
    user-select: none;
}

.slide-illustration .circle-pattern {
    position: absolute;
    border-radius: 50%;
    border: 1px solid currentColor;
    opacity: 0.02;
}

/* ── Reduced Motion ───────────────────────────────────────────── */

@media (prefers-reduced-motion: reduce) {
    .step-enter,
    .step-enter.step-enter-active,
    .step-exit,
    .step-exit-active,
    .step-enter-back,
    .step-enter-back.step-enter-active {
        transition: none !important;
        opacity: 1 !important;
        transform: none !important;
    }

    .progress-dot.pulse {
        animation: none;
    }

    .intent-card {
        transition: none;
    }

    .intent-card.selected {
        animation: none;
        transform: scale(1.02);
    }

    .carousel-track {
        transition: none;
    }

    .checkmark-svg.animate {
        animation: none;
        stroke-dashoffset: 0;
    }

    .spotlight-hole::before {
        animation: none;
        opacity: 0;
    }

    .spotlight-tooltip,
    .spotlight-sheet {
        transition: none;
    }

    .confetti-piece.animate {
        animation: none;
        display: none;
    }

    .location-fields {
        transition: none;
    }

    .photo-upload-area {
        transition: none;
    }

    .floating-field label {
        transition: none;
    }

    .field-error {
        animation: none;
    }
}
```

- [ ] **Step 2: Build verification**

No build needed for CSS. Verify the file exists:
```bash
ls -la wwwroot/css/onboarding.css
```

---

## Task 4: Onboarding Layout Shell

**Files:**
- Create: `Views/Shared/_OnboardingLayout.cshtml`

- [ ] **Step 1: Create `_OnboardingLayout.cshtml`**

```html
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer
@inject RentMate.Services.Interfaces.ICurrencyService CurrencyService
<!DOCTYPE html>
<html lang="en" class="scroll-smooth">
<head>
    <script>
        (function() {
            var t = localStorage.getItem('theme') || 'system';
            var dark = t === 'dark' || (t === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
            if (dark) document.documentElement.classList.add('dark');
        })();
    </script>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - RentMate</title>

    <!-- Tailwind CSS -->
    <script src="https://cdn.tailwindcss.com"></script>
    <script>
        tailwind.config = {
            darkMode: 'class',
            theme: {
                extend: {
                    fontFamily: {
                        'heading': ['Outfit', 'sans-serif'],
                        'body': ['Plus Jakarta Sans', 'sans-serif'],
                    },
                    colors: {
                        'trust-blue': {
                            50: '#eff6ff', 100: '#dbeafe', 200: '#bfdbfe', 300: '#93c5fd',
                            400: '#60a5fa', 500: '#3b82f6', 600: '#2563eb', 700: '#1d4ed8',
                            800: '#1e40af', 900: '#1e3a8a',
                        }
                    }
                }
            }
        }
    </script>

    <!-- Google Fonts -->
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700;800&family=Plus+Jakarta+Sans:wght@300;400;500;600;700&display=swap" rel="stylesheet">

    <!-- Icons -->
    <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.css" rel="stylesheet" crossorigin="anonymous">

    <!-- Theme + Onboarding CSS -->
    <link rel="stylesheet" href="~/css/theme.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/onboarding.css" asp-append-version="true" />

    <script>
        window.CurrentCurrency = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(CurrencyService.GetCurrentCurrency()));
    </script>
</head>
<body class="onboarding-bg font-body antialiased dark:text-slate-100">
    <!-- Minimal top bar: logo + logout -->
    <header class="fixed top-0 left-0 right-0 px-4 sm:px-6 lg:px-8 py-4 flex items-center justify-between" style="z-index: var(--z-navbar, 900);">
        <div class="flex items-center gap-2">
            <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-blue-600 to-sky-400 flex items-center justify-center shadow-sm">
                <i class="bi bi-house-heart text-white text-base"></i>
            </div>
            <span class="font-heading font-bold text-lg text-slate-900 dark:text-white">Rent<span class="bg-gradient-to-r from-blue-600 to-sky-400 bg-clip-text text-transparent">Mate</span></span>
        </div>
        <form asp-area="Identity" asp-page="/Account/Logout" asp-route-returnUrl="/" method="post">
            <button type="submit" class="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 transition-colors">
                @Localizer["Log out"]
            </button>
        </form>
    </header>

    <!-- Centered content area -->
    <main class="min-h-screen flex items-center justify-center px-4 sm:px-6 pt-20 pb-8">
        <div class="w-full max-w-xl">
            @RenderBody()
        </div>
    </main>

    <!-- Scripts -->
    <script src="~/js/site.js" asp-append-version="true"></script>
    <script src="~/js/translations.js" asp-append-version="true"></script>
    <script src="~/js/onboarding.js" asp-append-version="true"></script>

    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

- [ ] **Step 2: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeds (layout is just a Razor template, no compile errors unless inject types are wrong).

---

## Task 5: Controller Rewrite

**Files:**
- Modify: `Controllers/Mvc/OnboardingController.cs` (full rewrite)

- [ ] **Step 1: Rewrite `OnboardingController.cs`**

Replace the entire file content with:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentMate.Helpers;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Services.Interfaces;

namespace RentMate.Controllers.Mvc;

/// <summary>
/// Post-registration onboarding wizard (4 steps + completion).
/// Step 1: Welcome + Intent selection
/// Step 2: Name + optional Location
/// Step 3: Photo + Bio (optional, skippable)
/// Step 4: App Tour Carousel
/// </summary>
[Authorize]
public class OnboardingController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IFileUploadService _fileUploadService;
    private readonly RentMateContext _db;

    private const string ProfileImagesFolder = "profiles";

    public OnboardingController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IFileUploadService fileUploadService,
        RentMateContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _fileUploadService = fileUploadService;
        _db = db;
    }

    #region Step 1: Welcome + Intent

    [HttpGet]
    public async Task<IActionResult> Step1()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        return View(new OnboardingStep1ViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step1(OnboardingStep1ViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid || model.SelectedIntent == null)
            return View(model);

        user.UserIntent = model.SelectedIntent;
        await _userManager.UpdateAsync(user);

        return RedirectToAction(nameof(Step2));
    }

    #endregion

    #region Step 2: Name + Location

    [HttpGet]
    public async Task<IActionResult> Step2()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        // Guard: must complete Step 1 (intent)
        if (user.UserIntent == null)
            return RedirectToAction(nameof(Step1));

        var model = new OnboardingStep2ViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            City = user.City,
            CityOptions = BuildCityOptions(user.City)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step2(OnboardingStep2ViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            model.CityOptions = BuildCityOptions(model.City);
            return View(model);
        }

        user.FirstName = model.FirstName?.Trim();
        user.LastName = model.LastName?.Trim();

        if (model.ShareLocation && !string.IsNullOrEmpty(model.City))
        {
            // Validate city against allowlist
            if (!CityData.Cities.Any(c => c.Name == model.City))
            {
                ModelState.AddModelError(nameof(model.City), "Invalid city selection.");
                model.CityOptions = BuildCityOptions(model.City);
                return View(model);
            }

            user.City = model.City;
            var coords = CityData.GetCoordinates(model.City);
            if (coords.Lat != 0 || coords.Lng != 0)
            {
                user.Latitude = coords.Lat;
                user.Longitude = coords.Lng;
            }
        }
        else
        {
            // User declined location sharing
            user.City = null;
            user.Latitude = null;
            user.Longitude = null;
        }

        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Step3));
    }

    #endregion

    #region Step 3: Photo + Bio

    [HttpGet]
    public async Task<IActionResult> Step3()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        // Guard: must complete Step 2 (name)
        if (string.IsNullOrWhiteSpace(user.FirstName))
            return RedirectToAction(nameof(Step1));

        var model = new OnboardingStep3ViewModel
        {
            ExistingProfilePictureUrl = user.ProfilePictureUrl,
            Bio = user.Bio
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step3(OnboardingStep3ViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            model.ExistingProfilePictureUrl = user.ProfilePictureUrl;
            return View(model);
        }

        // Upload photo if provided
        if (model.ProfilePicture != null)
        {
            var url = await _fileUploadService.UploadFileAsync(model.ProfilePicture, ProfileImagesFolder);
            user.ProfilePictureUrl = url;
        }

        // Save bio if provided
        if (!string.IsNullOrWhiteSpace(model.Bio))
        {
            user.Bio = model.Bio.Trim();
        }

        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Step4));
    }

    #endregion

    #region Step 4: Carousel Tour

    [HttpGet]
    public async Task<IActionResult> Step4()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        // Guard: must complete Step 2 (name)
        if (string.IsNullOrWhiteSpace(user.FirstName))
            return RedirectToAction(nameof(Step1));

        var memberCount = await _db.Users.CountAsync();

        var model = new OnboardingStep4ViewModel
        {
            UserIntent = user.UserIntent ?? UserIntent.Both,
            FirstName = user.FirstName ?? "there",
            City = user.City,
            ShareLocation = !string.IsNullOrEmpty(user.City),
            MemberCount = memberCount
        };

        return View(model);
    }

    #endregion

    #region Complete Onboarding

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteOnboarding()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        user.OnboardingCompleted = true;
        await _userManager.UpdateAsync(user);

        // Refresh claims so OnboardingCompleted is up to date
        await _signInManager.RefreshSignInAsync(user);

        // Signal spotlight tour for the homepage
        TempData["ShowSpotlightTour"] = "true";
        TempData["SpotlightIntent"] = (user.UserIntent ?? UserIntent.Both).ToString();

        return RedirectToAction("Index", "Home");
    }

    #endregion

    #region Helpers

    private static List<SelectListItem> BuildCityOptions(string? selectedCity)
    {
        return CityData.Cities.Select(c => new SelectListItem
        {
            Value = c.Name,
            Text = c.Name,
            Selected = c.Name == selectedCity
        }).ToList();
    }

    #endregion
}
```

- [ ] **Step 2: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeds. Views will reference view models that exist in `OnboardingViewModels.cs`.

---

## Task 6: Step 1 View (Welcome + Intent)

**Files:**
- Modify: `Views/Onboarding/Step1.cshtml` (full rewrite)

- [ ] **Step 1: Rewrite `Step1.cshtml`**

Replace the entire file content with:

```html
@model RentMate.Models.ViewModels.OnboardingStep1ViewModel
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer
@{
    ViewData["Title"] = Localizer["Welcome"].Value;
    Layout = "~/Views/Shared/_OnboardingLayout.cshtml";
}

<!-- Progress Dots -->
<div class="flex items-center justify-center gap-2 mb-8" role="progressbar" aria-label="@Localizer["Step {0} of {1}", 1, 4]">
    <div class="progress-dot active pulse"></div>
    <div class="progress-dot"></div>
    <div class="progress-dot"></div>
    <div class="progress-dot"></div>
</div>

<div class="onboarding-step text-center">
    <!-- Hero illustration placeholder -->
    <div class="step-enter step-enter-delay-0 mx-auto mb-6 w-48 h-36 sm:w-56 sm:h-40 rounded-2xl bg-gradient-to-br from-blue-100 to-sky-50 dark:from-blue-950/30 dark:to-sky-950/20 flex items-center justify-center">
        <i class="bi bi-people text-4xl text-blue-400 dark:text-blue-500"></i>
    </div>

    <!-- Heading -->
    <h1 class="step-enter step-enter-delay-0 font-heading text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mb-2">
        @Localizer["Welcome to RentMate"]
    </h1>

    <!-- Value proposition -->
    <p class="step-enter step-enter-delay-1 text-slate-500 dark:text-slate-400 mb-8 max-w-md mx-auto">
        @Localizer["Rent anything from people around you, or earn by sharing what you own."]
    </p>

    <!-- Intent question -->
    <h2 class="step-enter step-enter-delay-1 font-heading text-lg font-semibold text-slate-800 dark:text-slate-200 mb-4">
        @Localizer["What brings you here?"]
    </h2>

    <!-- Intent Cards -->
    <form method="post" asp-action="Step1" id="intentForm">
        @Html.AntiForgeryToken()
        <input type="hidden" name="SelectedIntent" id="selectedIntent" />

        <div class="step-enter step-enter-delay-2 grid grid-cols-1 sm:grid-cols-3 gap-3">
            <!-- Rent card -->
            <div class="intent-card relative p-4 sm:p-5 rounded-2xl border bg-blue-50/70 dark:bg-blue-950/30 border-blue-200 dark:border-blue-800/40 text-left sm:text-center"
                 data-intent="Renter"
                 onclick="selectIntent(this, 'Renter')">
                <div class="flex sm:flex-col items-center sm:items-center gap-3">
                    <div class="w-10 h-10 rounded-xl bg-blue-100 dark:bg-blue-900/50 flex items-center justify-center flex-shrink-0">
                        <i class="bi bi-search text-blue-600 dark:text-blue-400 text-lg"></i>
                    </div>
                    <div>
                        <p class="font-semibold text-slate-900 dark:text-white text-sm">@Localizer["I want to rent"]</p>
                        <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">@Localizer["Find and rent items from people near you"]</p>
                    </div>
                </div>
                <div class="intent-check absolute top-2 right-2">
                    <div class="w-5 h-5 rounded-full bg-blue-600 flex items-center justify-center">
                        <i class="bi bi-check text-white text-xs"></i>
                    </div>
                </div>
            </div>

            <!-- List card -->
            <div class="intent-card relative p-4 sm:p-5 rounded-2xl border bg-emerald-50/70 dark:bg-emerald-950/30 border-emerald-200 dark:border-emerald-800/40 text-left sm:text-center"
                 data-intent="Lister"
                 onclick="selectIntent(this, 'Lister')">
                <div class="flex sm:flex-col items-center sm:items-center gap-3">
                    <div class="w-10 h-10 rounded-xl bg-emerald-100 dark:bg-emerald-900/50 flex items-center justify-center flex-shrink-0">
                        <i class="bi bi-box-seam text-emerald-600 dark:text-emerald-400 text-lg"></i>
                    </div>
                    <div>
                        <p class="font-semibold text-slate-900 dark:text-white text-sm">@Localizer["I want to list"]</p>
                        <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">@Localizer["Earn money by sharing your stuff"]</p>
                    </div>
                </div>
                <div class="intent-check absolute top-2 right-2">
                    <div class="w-5 h-5 rounded-full bg-blue-600 flex items-center justify-center">
                        <i class="bi bi-check text-white text-xs"></i>
                    </div>
                </div>
            </div>

            <!-- Both card -->
            <div class="intent-card relative p-4 sm:p-5 rounded-2xl border bg-amber-50/70 dark:bg-amber-950/30 border-amber-200 dark:border-amber-800/40 text-left sm:text-center"
                 data-intent="Both"
                 onclick="selectIntent(this, 'Both')">
                <div class="flex sm:flex-col items-center sm:items-center gap-3">
                    <div class="w-10 h-10 rounded-xl bg-amber-100 dark:bg-amber-900/50 flex items-center justify-center flex-shrink-0">
                        <i class="bi bi-arrow-left-right text-amber-600 dark:text-amber-400 text-lg"></i>
                    </div>
                    <div>
                        <p class="font-semibold text-slate-900 dark:text-white text-sm">@Localizer["Both"]</p>
                        <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">@Localizer["Rent and list, best of both worlds"]</p>
                    </div>
                </div>
                <div class="intent-check absolute top-2 right-2">
                    <div class="w-5 h-5 rounded-full bg-blue-600 flex items-center justify-center">
                        <i class="bi bi-check text-white text-xs"></i>
                    </div>
                </div>
            </div>
        </div>
    </form>
</div>

@section Scripts {
<script>
    // Trigger staggered entrance
    document.addEventListener('DOMContentLoaded', function() {
        requestAnimationFrame(function() {
            document.querySelectorAll('.step-enter').forEach(function(el) {
                el.classList.add('step-enter-active');
            });
        });
    });

    function selectIntent(card, intent) {
        // Remove previous selection
        document.querySelectorAll('.intent-card').forEach(function(c) {
            c.classList.remove('selected');
        });

        // Mark selected
        card.classList.add('selected');
        document.getElementById('selectedIntent').value = intent;

        // Auto-advance after 400ms
        setTimeout(function() {
            document.getElementById('intentForm').submit();
        }, 400);
    }
</script>
}
```

- [ ] **Step 2: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeds.

---

## Task 7: Step 2 View (Name + Location)

**Files:**
- Modify: `Views/Onboarding/Step2.cshtml` (full rewrite)

- [ ] **Step 1: Rewrite `Step2.cshtml`**

Replace the entire file content with:

```html
@model RentMate.Models.ViewModels.OnboardingStep2ViewModel
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer
@{
    ViewData["Title"] = Localizer["About You"].Value;
    Layout = "~/Views/Shared/_OnboardingLayout.cshtml";
}

<!-- Progress Dots -->
<div class="flex items-center justify-center gap-2 mb-8" role="progressbar" aria-label="@Localizer["Step {0} of {1}", 2, 4]">
    <div class="progress-dot completed"></div>
    <div class="progress-dot active pulse"></div>
    <div class="progress-dot"></div>
    <div class="progress-dot"></div>
</div>

<div class="onboarding-step">
    <!-- Heading -->
    <h1 class="step-enter step-enter-delay-0 font-heading text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mb-2 text-center">
        @Localizer["Tell us about yourself"]
    </h1>
    <p class="step-enter step-enter-delay-1 text-slate-500 dark:text-slate-400 mb-8 text-center">
        @Localizer["This helps build trust with other members."]
    </p>

    <form method="post" asp-action="Step2" id="step2Form" novalidate>
        @Html.AntiForgeryToken()

        <!-- Name Fields -->
        <div class="step-enter step-enter-delay-2 grid grid-cols-1 sm:grid-cols-2 gap-4 mb-6">
            <!-- First Name -->
            <div class="floating-field">
                <input type="text"
                       name="FirstName"
                       id="firstName"
                       value="@Model.FirstName"
                       placeholder=" "
                       required
                       maxlength="50"
                       class="w-full px-3 pt-5 pb-2 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-0" />
                <label for="firstName">@Localizer["First Name"]</label>
                <span class="validation-error hidden text-red-500 text-xs mt-1" data-for="firstName">@Localizer["First name is required."]</span>
            </div>

            <!-- Last Name -->
            <div class="floating-field">
                <input type="text"
                       name="LastName"
                       id="lastName"
                       value="@Model.LastName"
                       placeholder=" "
                       required
                       maxlength="50"
                       class="w-full px-3 pt-5 pb-2 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-0" />
                <label for="lastName">@Localizer["Last Name"]</label>
                <span class="validation-error hidden text-red-500 text-xs mt-1" data-for="lastName">@Localizer["Last name is required."]</span>
            </div>
        </div>

        <!-- Divider -->
        <hr class="step-enter step-enter-delay-2 border-slate-200 dark:border-slate-700 mb-6" />

        <!-- Location Section -->
        <div class="step-enter step-enter-delay-2">
            <div class="flex items-center justify-between mb-3">
                <div class="flex items-center gap-2">
                    <h2 class="font-heading font-semibold text-slate-900 dark:text-white">@Localizer["Location"]</h2>
                    <span class="text-xs px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-500 dark:text-slate-400">@Localizer["Optional"]</span>
                </div>
                <!-- Toggle (hidden input ensures false is posted when unchecked) -->
                <label class="relative inline-flex items-center cursor-pointer">
                    <input type="hidden" name="ShareLocation" value="false" />
                    <input type="checkbox"
                           name="ShareLocation"
                           id="shareLocationToggle"
                           value="true"
                           @(Model.ShareLocation ? "checked" : "")
                           class="sr-only peer"
                           onchange="toggleLocation(this.checked)" />
                    <div class="w-9 h-5 bg-slate-300 peer-focus:ring-2 peer-focus:ring-blue-500 dark:bg-slate-600 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:start-[2px] after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-blue-600"></div>
                    <span class="ms-2 text-sm text-slate-600 dark:text-slate-400">@Localizer["Share location"]</span>
                </label>
            </div>

            <!-- Info box -->
            <div class="bg-blue-50 dark:bg-blue-950/30 border border-blue-100 dark:border-blue-900/40 rounded-xl p-3 mb-4 text-sm text-blue-700 dark:text-blue-300">
                <i class="bi bi-info-circle me-1"></i>
                @Localizer["Pick the area closest to you. This is used to recommend items nearby and show your approximate area to other users. Your exact address is never shared."]
            </div>

            <!-- Location fields (collapsible) -->
            <div id="locationFields" class="location-fields @(Model.ShareLocation ? "" : "collapsed")">
                <!-- Country (placeholder for future) -->
                <div class="mb-4">
                    <label for="country" class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1.5">@Localizer["Country"]</label>
                    <select name="Country" id="country"
                            class="w-full px-3 py-2.5 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                        <option value="Slovenia" selected>Slovenia</option>
                    </select>
                </div>

                <!-- State/Region (placeholder for future) -->
                <div class="mb-4">
                    <label for="state" class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1.5">@Localizer["State / Region"]</label>
                    <select name="State" id="state"
                            class="w-full px-3 py-2.5 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                        <option value="">@Localizer["-- Select a region --"]</option>
                    </select>
                </div>

                <!-- City -->
                <div class="mb-4">
                    <label for="city" class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1.5">@Localizer["City"]</label>
                    <select name="City" id="city"
                            class="w-full px-3 py-2.5 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                        <option value="">@Localizer["-- Select a city --"]</option>
                        @foreach (var opt in Model.CityOptions)
                        {
                            <option value="@opt.Value" @(opt.Selected ? "selected" : "")>@opt.Text</option>
                        }
                    </select>
                </div>
            </div>

            <!-- Location declined message -->
            <div id="locationDeclinedMsg" class="@(Model.ShareLocation ? "hidden" : "") text-sm text-slate-500 dark:text-slate-400 bg-slate-50 dark:bg-slate-800/50 rounded-xl p-3 mb-4">
                @Localizer["Without a location, you'll still be able to browse and use RentMate, but items won't be sorted by distance and your profile won't show an area. You can always add this later in settings."]
            </div>
        </div>

        <!-- Buttons -->
        <div class="step-enter step-enter-delay-3 flex items-center justify-between mt-8">
            <a asp-action="Step1" class="flex items-center gap-1 text-sm text-slate-500 dark:text-slate-400 hover:text-blue-600 dark:hover:text-blue-400 transition-colors">
                <i class="bi bi-arrow-left"></i> @Localizer["Back"]
            </a>
            <button type="submit" id="continueBtn"
                    class="px-6 py-2.5 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white rounded-xl font-medium transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                    disabled>
                @Localizer["Continue"]
            </button>
        </div>
    </form>
</div>

@section Scripts {
<script>
    document.addEventListener('DOMContentLoaded', function() {
        // Trigger staggered entrance
        requestAnimationFrame(function() {
            document.querySelectorAll('.step-enter').forEach(function(el) {
                el.classList.add('step-enter-active');
            });
        });

        // Enable/disable continue button based on name fields
        var firstName = document.getElementById('firstName');
        var lastName = document.getElementById('lastName');
        var continueBtn = document.getElementById('continueBtn');

        function checkNames() {
            var valid = firstName.value.trim().length > 0 && lastName.value.trim().length > 0;
            continueBtn.disabled = !valid;
        }

        firstName.addEventListener('input', checkNames);
        lastName.addEventListener('input', checkNames);
        checkNames(); // Initial check
    });

    function toggleLocation(checked) {
        var fields = document.getElementById('locationFields');
        var msg = document.getElementById('locationDeclinedMsg');
        if (checked) {
            fields.classList.remove('collapsed');
            msg.classList.add('hidden');
        } else {
            fields.classList.add('collapsed');
            msg.classList.remove('hidden');
        }
    }
</script>
}
```

- [ ] **Step 2: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeds.

---

## Task 8: Step 3 View (Photo + Bio)

**Files:**
- Create: `Views/Onboarding/Step3.cshtml`

- [ ] **Step 1: Create the file**

Create `Views/Onboarding/Step3.cshtml` with:

```html
@model RentMate.Models.ViewModels.OnboardingStep3ViewModel
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer
@{
    ViewData["Title"] = Localizer["Profile Photo"].Value;
    Layout = "~/Views/Shared/_OnboardingLayout.cshtml";
}

<!-- Progress Dots -->
<div class="flex items-center justify-center gap-2 mb-8" role="progressbar" aria-label="@Localizer["Step {0} of {1}", 3, 4]">
    <div class="progress-dot completed"></div>
    <div class="progress-dot completed"></div>
    <div class="progress-dot active pulse"></div>
    <div class="progress-dot"></div>
</div>

<div class="onboarding-step">
    <!-- Heading -->
    <h1 id="photoHeading" class="step-enter step-enter-delay-0 font-heading text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mb-2 text-center">
        @Localizer["Add a profile photo"]
    </h1>
    <p class="step-enter step-enter-delay-1 text-slate-500 dark:text-slate-400 mb-6 text-center">
        @Localizer["People are more likely to rent from someone they can see."]
    </p>

    <form method="post" asp-action="Step3" enctype="multipart/form-data" id="step3Form">
        @Html.AntiForgeryToken()

        <!-- Photo Upload Area -->
        <div class="step-enter step-enter-delay-2 flex flex-col items-center mb-6">
            <div id="photoUploadArea"
                 class="photo-upload-area relative w-[140px] h-[140px] sm:w-[180px] sm:h-[180px] rounded-full border-2 border-dashed border-slate-300 dark:border-slate-600 flex flex-col items-center justify-center cursor-pointer overflow-hidden"
                 onclick="document.getElementById('photoInput').click()">

                <!-- Default state -->
                <div id="uploadPrompt" class="flex flex-col items-center gap-1 text-slate-400 dark:text-slate-500">
                    <i class="bi bi-camera text-2xl"></i>
                    <span class="text-xs font-medium hidden sm:block">@Localizer["Drag & drop"]</span>
                    <span class="text-[10px] hidden sm:block text-slate-400 dark:text-slate-500">@Localizer["or click to browse"]</span>
                    <span class="text-xs sm:hidden">@Localizer["Tap to upload"]</span>
                </div>

                <!-- Drag hover state text -->
                <div id="dropHereText" class="hidden flex flex-col items-center gap-1 text-blue-600 dark:text-blue-400">
                    <i class="bi bi-cloud-arrow-down text-2xl"></i>
                    <span class="text-xs font-medium">@Localizer["Drop it here!"]</span>
                </div>

                <!-- Preview image (hidden until file selected) -->
                <img id="photoPreview" src="@Model.ExistingProfilePictureUrl" alt=""
                     class="@(string.IsNullOrEmpty(Model.ExistingProfilePictureUrl) ? "hidden" : "") absolute inset-0 w-full h-full object-cover" />

                <!-- Uploaded checkmark overlay -->
                <div id="uploadedOverlay" class="@(string.IsNullOrEmpty(Model.ExistingProfilePictureUrl) ? "hidden" : "") absolute inset-0 bg-black/20 flex items-center justify-center">
                    <div class="w-8 h-8 rounded-full bg-green-500 flex items-center justify-center">
                        <i class="bi bi-check-lg text-white"></i>
                    </div>
                </div>
            </div>

            <input type="file"
                   name="ProfilePicture"
                   id="photoInput"
                   accept=".jpg,.jpeg,.png,.webp"
                   class="hidden"
                   onchange="handlePhotoSelect(this)" />

            <!-- Change photo link (shown after upload) -->
            <button type="button" id="changePhotoBtn"
                    class="@(string.IsNullOrEmpty(Model.ExistingProfilePictureUrl) ? "hidden" : "") mt-2 text-sm text-blue-600 dark:text-blue-400 hover:underline"
                    onclick="document.getElementById('photoInput').click()">
                @Localizer["Change photo"]
            </button>

            <!-- Social proof -->
            <p class="mt-3 text-xs text-slate-400 dark:text-slate-500">
                <i class="bi bi-star-fill text-amber-400 me-1"></i>
                @Localizer["Profiles with photos get 3x more responses"]
            </p>

            <!-- File constraints -->
            <p class="mt-1 text-xs text-slate-400 dark:text-slate-500">
                JPG, PNG, WebP. @Localizer["Max {0}MB", 5]
            </p>
        </div>

        <!-- Divider -->
        <hr class="step-enter step-enter-delay-2 border-slate-200 dark:border-slate-700 mb-6" />

        <!-- Bio Textarea -->
        <div class="step-enter step-enter-delay-2 mb-6">
            <div class="flex items-center justify-between mb-1.5">
                <label for="bioInput" class="text-sm font-medium text-slate-700 dark:text-slate-300">@Localizer["About you"]</label>
                <span class="text-xs px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-500 dark:text-slate-400">@Localizer["Optional"]</span>
            </div>
            <textarea name="Bio"
                      id="bioInput"
                      maxlength="500"
                      rows="3"
                      placeholder="@Localizer["Tell others a bit about yourself..."]"
                      class="w-full px-3 py-2.5 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none transition-colors"
                      oninput="updateCharCount()">@Model.Bio</textarea>
            <div class="flex justify-end mt-1">
                <span id="charCount" class="text-xs text-slate-400">0 / 500</span>
            </div>
        </div>

        <!-- Buttons -->
        <div class="step-enter step-enter-delay-3 flex items-center justify-between">
            <a asp-action="Step2" class="flex items-center gap-1 text-sm text-slate-500 dark:text-slate-400 hover:text-blue-600 dark:hover:text-blue-400 transition-colors">
                <i class="bi bi-arrow-left"></i> @Localizer["Back"]
            </a>
            <div class="flex items-center gap-3">
                <a asp-action="Step4" class="text-sm text-slate-500 dark:text-slate-400 hover:text-blue-600 dark:hover:text-blue-400 transition-colors">
                    @Localizer["Skip for now"]
                </a>
                <button type="submit"
                        class="px-6 py-2.5 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white rounded-xl font-medium transition-all">
                    @Localizer["Continue"]
                </button>
            </div>
        </div>
    </form>
</div>

@section Scripts {
<script>
    document.addEventListener('DOMContentLoaded', function() {
        // Trigger staggered entrance
        requestAnimationFrame(function() {
            document.querySelectorAll('.step-enter').forEach(function(el) {
                el.classList.add('step-enter-active');
            });
        });

        updateCharCount();
        initDragDrop();
    });

    function handlePhotoSelect(input) {
        if (!input.files || !input.files[0]) return;
        var file = input.files[0];

        // 5MB validation
        if (file.size > 5 * 1024 * 1024) {
            if (typeof showToast === 'function') showToast(window.T?.FileTooLarge || 'File too large. Maximum 5MB.', 'error');
            input.value = '';
            return;
        }

        var reader = new FileReader();
        reader.onload = function(e) {
            var preview = document.getElementById('photoPreview');
            var prompt = document.getElementById('uploadPrompt');
            var overlay = document.getElementById('uploadedOverlay');
            var changeBtn = document.getElementById('changePhotoBtn');
            var heading = document.getElementById('photoHeading');

            preview.src = e.target.result;
            preview.classList.remove('hidden');
            prompt.classList.add('hidden');
            overlay.classList.remove('hidden');
            changeBtn.classList.remove('hidden');

            // Update heading
            heading.textContent = window.T?.LookingGood || 'Looking good!';

            // Update border style
            var area = document.getElementById('photoUploadArea');
            area.classList.remove('border-dashed', 'border-slate-300', 'dark:border-slate-600');
            area.classList.add('border-solid', 'border-blue-500');
        };
        reader.readAsDataURL(file);
    }

    function updateCharCount() {
        var textarea = document.getElementById('bioInput');
        var counter = document.getElementById('charCount');
        var len = textarea.value.length;
        counter.textContent = len + ' / 500';
        counter.classList.remove('text-amber-500', 'text-red-500', 'text-slate-400');
        if (len >= 480) counter.classList.add('text-red-500');
        else if (len >= 400) counter.classList.add('text-amber-500');
        else counter.classList.add('text-slate-400');
    }

    function initDragDrop() {
        var area = document.getElementById('photoUploadArea');
        var prompt = document.getElementById('uploadPrompt');
        var dropText = document.getElementById('dropHereText');

        ['dragenter', 'dragover'].forEach(function(evt) {
            area.addEventListener(evt, function(e) {
                e.preventDefault();
                e.stopPropagation();
                area.classList.add('drag-over');
                prompt.classList.add('hidden');
                dropText.classList.remove('hidden');
            });
        });

        ['dragleave', 'drop'].forEach(function(evt) {
            area.addEventListener(evt, function(e) {
                e.preventDefault();
                e.stopPropagation();
                area.classList.remove('drag-over');
                dropText.classList.add('hidden');
                // Only show prompt if no image is selected
                if (document.getElementById('photoPreview').classList.contains('hidden')) {
                    prompt.classList.remove('hidden');
                }
            });
        });

        area.addEventListener('drop', function(e) {
            var files = e.dataTransfer.files;
            if (files.length > 0) {
                var input = document.getElementById('photoInput');
                input.files = files;
                handlePhotoSelect(input);
            }
        });
    }
</script>
}
```

- [ ] **Step 2: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeds.

---

## Task 9: Step 4 View (Carousel Tour)

**Files:**
- Create: `Views/Onboarding/Step4.cshtml`

- [ ] **Step 1: Create the file**

Create `Views/Onboarding/Step4.cshtml` with:

```html
@model RentMate.Models.ViewModels.OnboardingStep4ViewModel
@using Microsoft.AspNetCore.Mvc.Localization
@using RentMate.Models.Domain
@inject IViewLocalizer Localizer
@{
    ViewData["Title"] = Localizer["App Tour"].Value;
    Layout = "~/Views/Shared/_OnboardingLayout.cshtml";

    var isRenter = Model.UserIntent == UserIntent.Renter;
    var isLister = Model.UserIntent == UserIntent.Lister;
    var isBoth = Model.UserIntent == UserIntent.Both;

    // Social proof text
    var socialProof = Model.ShareLocation && !string.IsNullOrEmpty(Model.City)
        ? Localizer["Join {0} other members in {1}", Model.MemberCount, Model.City].Value
        : Localizer["Join {0} other members on RentMate", Model.MemberCount].Value;

    // CTA text per intent
    var ctaText = isRenter ? Localizer["Browse items near you"].Value
        : isLister ? Localizer["List your first item"].Value
        : Localizer["Explore RentMate"].Value;
}

<!-- Progress Dots -->
<div class="flex items-center justify-center gap-2 mb-6" role="progressbar" aria-label="@Localizer["Step {0} of {1}", 4, 4]">
    <div class="progress-dot completed"></div>
    <div class="progress-dot completed"></div>
    <div class="progress-dot completed"></div>
    <div class="progress-dot active pulse"></div>
</div>

<div class="onboarding-step">
    <!-- Section label (Both intent only) -->
    @if (isBoth)
    {
        <div id="sectionLabel" class="text-center mb-3">
            <span class="text-xs font-bold tracking-wider px-3 py-1 rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400">
                @Localizer["RENTING ITEMS"]
            </span>
        </div>
    }

    <!-- Carousel Container -->
    <div class="relative overflow-hidden rounded-2xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm" id="carouselContainer">
        <div class="carousel-track" id="carouselTrack">
            @* ── Renter Slides ── *@
            @if (isRenter || isBoth)
            {
                <!-- Renter Slide 1 -->
                <div class="carousel-slide p-6" data-section="renter">
                    <div class="slide-illustration bg-gradient-to-br from-blue-100 to-sky-50 dark:from-blue-950/40 dark:to-sky-950/20 mb-4 text-blue-300 dark:text-blue-700">
                        <span class="watermark">01</span>
                        <i class="bi bi-search text-4xl text-blue-500 dark:text-blue-400 relative" style="z-index:1"></i>
                    </div>
                    <h3 class="font-heading text-lg font-bold text-slate-900 dark:text-white mb-2">@Localizer["Find what you need"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400">@Localizer["Search by category, location, or keyword. Filter by price, distance, and availability to find exactly what you're looking for."]</p>
                </div>

                <!-- Renter Slide 2 -->
                <div class="carousel-slide p-6" data-section="renter">
                    <div class="slide-illustration bg-gradient-to-br from-blue-100 to-sky-50 dark:from-blue-950/40 dark:to-sky-950/20 mb-4 text-blue-300 dark:text-blue-700">
                        <span class="watermark">02</span>
                        <i class="bi bi-calendar-check text-4xl text-blue-500 dark:text-blue-400 relative" style="z-index:1"></i>
                    </div>
                    <h3 class="font-heading text-lg font-bold text-slate-900 dark:text-white mb-2">@Localizer["Rent with confidence"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400">@Localizer["Pick your dates, review the price breakdown, and send a rental request. The owner confirms and you're all set."]</p>
                </div>

                <!-- Renter Slide 3 -->
                <div class="carousel-slide p-6" data-section="renter">
                    <div class="slide-illustration bg-gradient-to-br from-blue-100 to-sky-50 dark:from-blue-950/40 dark:to-sky-950/20 mb-4 text-blue-300 dark:text-blue-700">
                        <span class="watermark">03</span>
                        <i class="bi bi-shield-check text-4xl text-blue-500 dark:text-blue-400 relative" style="z-index:1"></i>
                    </div>
                    <h3 class="font-heading text-lg font-bold text-slate-900 dark:text-white mb-2">@Localizer["Secure deposits"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400">@Localizer["Deposits protect both sides. They're held safely and released automatically when the rental ends without issues."]</p>
                </div>

                <!-- Renter Slide 4 -->
                <div class="carousel-slide p-6" data-section="renter">
                    <div class="slide-illustration bg-gradient-to-br from-blue-100 to-sky-50 dark:from-blue-950/40 dark:to-sky-950/20 mb-4 text-blue-300 dark:text-blue-700">
                        <span class="watermark">04</span>
                        <i class="bi bi-star text-4xl text-blue-500 dark:text-blue-400 relative" style="z-index:1"></i>
                    </div>
                    <h3 class="font-heading text-lg font-bold text-slate-900 dark:text-white mb-2">@Localizer["Reviews build trust"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400">@Localizer["After each rental, both sides leave a review. Verified reviews help the community stay trustworthy."]</p>
                </div>
            }

            @* ── Transition Slide (Both intent only) ── *@
            @if (isBoth)
            {
                <div class="carousel-slide p-6 flex flex-col items-center justify-center text-center" data-section="transition">
                    <svg class="checkmark-svg w-16 h-16 mb-4" viewBox="0 0 52 52" fill="none" stroke="#22c55e" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                        <circle cx="26" cy="26" r="22" stroke-dasharray="138" stroke-dashoffset="138" class="checkmark-svg" />
                        <path d="M15 27l7 7 15-15" stroke-dasharray="40" stroke-dashoffset="40" class="checkmark-svg" />
                    </svg>
                    <h3 class="font-heading text-xl font-bold text-slate-900 dark:text-white mb-2">@Localizer["That's how renting works!"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400 mb-4">@Localizer["Now let's look at the other side: listing your items and earning money."]</p>
                    <button type="button" onclick="carouselNext()"
                            class="px-5 py-2 bg-gradient-to-r from-emerald-600 to-emerald-500 hover:from-emerald-700 hover:to-emerald-600 text-white rounded-xl text-sm font-medium transition-all">
                        @Localizer["Show me"]
                    </button>
                </div>
            }

            @* ── Lister Slides ── *@
            @if (isLister || isBoth)
            {
                <!-- Lister Slide 1 -->
                <div class="carousel-slide p-6" data-section="lister">
                    <div class="slide-illustration bg-gradient-to-br from-emerald-100 to-green-50 dark:from-emerald-950/40 dark:to-green-950/20 mb-4 text-emerald-300 dark:text-emerald-700">
                        <span class="watermark">01</span>
                        <i class="bi bi-plus-circle text-4xl text-emerald-500 dark:text-emerald-400 relative" style="z-index:1"></i>
                    </div>
                    <h3 class="font-heading text-lg font-bold text-slate-900 dark:text-white mb-2">@Localizer["Create a listing"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400">@Localizer["Add photos, set your price, and describe your item. It only takes a few minutes to go live."]</p>
                </div>

                <!-- Lister Slide 2 -->
                <div class="carousel-slide p-6" data-section="lister">
                    <div class="slide-illustration bg-gradient-to-br from-emerald-100 to-green-50 dark:from-emerald-950/40 dark:to-green-950/20 mb-4 text-emerald-300 dark:text-emerald-700">
                        <span class="watermark">02</span>
                        <i class="bi bi-inbox text-4xl text-emerald-500 dark:text-emerald-400 relative" style="z-index:1"></i>
                    </div>
                    <h3 class="font-heading text-lg font-bold text-slate-900 dark:text-white mb-2">@Localizer["Manage requests"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400">@Localizer["Review rental requests from your dashboard. Accept, decline, or suggest different dates."]</p>
                </div>

                <!-- Lister Slide 3 -->
                <div class="carousel-slide p-6" data-section="lister">
                    <div class="slide-illustration bg-gradient-to-br from-emerald-100 to-green-50 dark:from-emerald-950/40 dark:to-green-950/20 mb-4 text-emerald-300 dark:text-emerald-700">
                        <span class="watermark">03</span>
                        <i class="bi bi-cash-stack text-4xl text-emerald-500 dark:text-emerald-400 relative" style="z-index:1"></i>
                    </div>
                    <h3 class="font-heading text-lg font-bold text-slate-900 dark:text-white mb-2">@Localizer["Earn money"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400">@Localizer["Get paid for each rental. Track your earnings and payouts from your dashboard."]</p>
                </div>

                <!-- Lister Slide 4 -->
                <div class="carousel-slide p-6" data-section="lister">
                    <div class="slide-illustration bg-gradient-to-br from-emerald-100 to-green-50 dark:from-emerald-950/40 dark:to-green-950/20 mb-4 text-emerald-300 dark:text-emerald-700">
                        <span class="watermark">04</span>
                        <i class="bi bi-trophy text-4xl text-emerald-500 dark:text-emerald-400 relative" style="z-index:1"></i>
                    </div>
                    <h3 class="font-heading text-lg font-bold text-slate-900 dark:text-white mb-2">@Localizer["Build your reputation"]</h3>
                    <p class="text-sm text-slate-500 dark:text-slate-400">@Localizer["Great reviews attract more renters. Respond quickly and keep items in good shape to grow your trust score."]</p>
                </div>
            }

            @* ── Final CTA Slide (all intents) ── *@
            <div class="carousel-slide p-6 flex flex-col items-center justify-center text-center" data-section="final">
                <svg class="checkmark-svg w-16 h-16 mb-4" viewBox="0 0 52 52" fill="none" stroke="#2563eb" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="26" cy="26" r="22" stroke-dasharray="138" stroke-dashoffset="138" class="checkmark-svg" />
                    <path d="M15 27l7 7 15-15" stroke-dasharray="40" stroke-dashoffset="40" class="checkmark-svg" />
                </svg>
                <h3 class="font-heading text-xl font-bold text-slate-900 dark:text-white mb-2">@Localizer["You're all set, {0}!", Model.FirstName]</h3>
                <p class="text-sm text-slate-500 dark:text-slate-400 mb-6">@socialProof</p>
                <form method="post" asp-action="CompleteOnboarding">
                    @Html.AntiForgeryToken()
                    <button type="submit"
                            class="px-8 py-3 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white rounded-xl font-semibold transition-all shadow-lg shadow-blue-500/25">
                        @ctaText
                    </button>
                </form>
            </div>
        </div>
    </div>

    <!-- Carousel Dots -->
    <div class="flex items-center justify-center gap-1.5 mt-4" id="carouselDots" aria-label="@Localizer["Carousel navigation"]">
        <!-- Dots are generated by JS based on slide count -->
    </div>

    <!-- Navigation -->
    <div class="flex items-center justify-between mt-4">
        <button type="button" id="skipTourBtn" onclick="skipTour()"
                class="text-sm text-slate-500 dark:text-slate-400 hover:text-blue-600 dark:hover:text-blue-400 transition-colors">
            @Localizer["Skip tour"]
        </button>
        <button type="button" id="nextBtn" onclick="carouselNext()"
                class="px-5 py-2 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white rounded-xl text-sm font-medium transition-all">
            @Localizer["Next"] <i class="bi bi-arrow-right ms-1"></i>
        </button>
    </div>
</div>

@section Scripts {
<script>
    var currentSlide = 0;
    var slides = document.querySelectorAll('.carousel-slide');
    var totalSlides = slides.length;
    var track = document.getElementById('carouselTrack');
    var dotsContainer = document.getElementById('carouselDots');
    var nextBtn = document.getElementById('nextBtn');
    var skipBtn = document.getElementById('skipTourBtn');
    var isBoth = @(isBoth ? "true" : "false");

    // Build dots
    (function() {
        for (var i = 0; i < totalSlides; i++) {
            var dot = document.createElement('div');
            dot.className = 'carousel-dot';
            if (slides[i].dataset.section === 'transition') dot.classList.add('transition-dot');
            dotsContainer.appendChild(dot);
        }
        updateCarousel();
    })();

    function updateCarousel() {
        track.style.transform = 'translateX(-' + (currentSlide * 100) + '%)';

        // Update dots
        var dots = dotsContainer.querySelectorAll('.carousel-dot');
        dots.forEach(function(dot, i) {
            dot.classList.remove('active', 'completed');
            if (i === currentSlide) dot.classList.add('active');
            else if (i < currentSlide) dot.classList.add('completed');
        });

        // Update section label for Both intent
        if (isBoth) {
            var label = document.getElementById('sectionLabel');
            if (label) {
                var section = slides[currentSlide].dataset.section;
                var span = label.querySelector('span');
                if (section === 'lister') {
                    span.textContent = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["LISTING YOUR ITEMS"].Value));
                    span.className = 'text-xs font-bold tracking-wider px-3 py-1 rounded-full bg-emerald-100 dark:bg-emerald-900/40 text-emerald-600 dark:text-emerald-400';
                } else if (section === 'transition' || section === 'final') {
                    label.classList.add('hidden');
                } else {
                    span.textContent = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["RENTING ITEMS"].Value));
                    span.className = 'text-xs font-bold tracking-wider px-3 py-1 rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400';
                    label.classList.remove('hidden');
                }
            }
        }

        // Hide Next/Skip on final slide, show on transition slide
        var isFinal = slides[currentSlide].dataset.section === 'final';
        var isTransition = slides[currentSlide].dataset.section === 'transition';
        nextBtn.classList.toggle('hidden', isFinal || isTransition);
        skipBtn.classList.toggle('hidden', isFinal);

        // Animate SVG checkmarks on transition/final slides
        if (isTransition || isFinal) {
            slides[currentSlide].querySelectorAll('.checkmark-svg').forEach(function(svg) {
                svg.classList.add('animate');
            });
        }
    }

    function carouselNext() {
        if (currentSlide < totalSlides - 1) {
            currentSlide++;
            updateCarousel();
        }
    }

    function carouselPrev() {
        if (currentSlide > 0) {
            currentSlide--;
            updateCarousel();
        }
    }

    function skipTour() {
        // Jump to final CTA slide
        currentSlide = totalSlides - 1;
        updateCarousel();
    }

    // Keyboard navigation
    document.addEventListener('keydown', function(e) {
        if (e.key === 'ArrowRight') carouselNext();
        else if (e.key === 'ArrowLeft') carouselPrev();
        else if (e.key === 'Enter' && slides[currentSlide].dataset.section === 'final') {
            var form = slides[currentSlide].querySelector('form');
            if (form) form.submit();
        }
    });

    // Touch swipe
    var touchStartX = 0;
    var carouselEl = document.getElementById('carouselContainer');
    carouselEl.addEventListener('touchstart', function(e) {
        touchStartX = e.changedTouches[0].screenX;
    }, { passive: true });
    carouselEl.addEventListener('touchend', function(e) {
        var diff = touchStartX - e.changedTouches[0].screenX;
        if (Math.abs(diff) > 50) {
            if (diff > 0) carouselNext();
            else carouselPrev();
        }
    }, { passive: true });

    // Entrance animation
    document.addEventListener('DOMContentLoaded', function() {
        requestAnimationFrame(function() {
            document.querySelectorAll('.step-enter').forEach(function(el) {
                el.classList.add('step-enter-active');
            });
        });
    });
</script>
}
```

- [ ] **Step 2: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeds.

---

## Task 10: Onboarding JavaScript

**Files:**
- Create: `wwwroot/js/onboarding.js`

- [ ] **Step 1: Create `onboarding.js`**

This file provides the spotlight tour functionality. The carousel, drag-drop, and form interactions are inline in each view's Scripts section (they need Razor-generated data). The spotlight tour runs on the homepage after onboarding completion.

```javascript
/**
 * onboarding.js
 * Spotlight tour for post-onboarding homepage walkthrough.
 * Loaded conditionally when TempData["ShowSpotlightTour"] is set.
 */
(function() {
    'use strict';

    // ── Spotlight Tour ──────────────────────────────────────────────

    window.initSpotlightTour = function(config) {
        if (!config || !config.stops || config.stops.length === 0) return;

        // Check if already completed
        if (localStorage.getItem('rentmate_spotlight_completed') === 'true') return;

        var stops = config.stops;
        var currentStop = 0;
        var overlay = null;
        var hole = null;
        var tooltip = null;
        var sheet = null;
        var isMobile = window.innerWidth < 768;

        function createOverlay() {
            overlay = document.createElement('div');
            overlay.className = 'spotlight-overlay';
            overlay.innerHTML = '<div class="spotlight-overlay-bg"></div>';

            hole = document.createElement('div');
            hole.className = 'spotlight-hole';
            overlay.appendChild(hole);

            // Desktop tooltip
            tooltip = document.createElement('div');
            tooltip.className = 'spotlight-tooltip';
            overlay.appendChild(tooltip);

            // Mobile sheet
            sheet = document.createElement('div');
            sheet.className = 'spotlight-sheet';
            overlay.appendChild(sheet);

            document.body.appendChild(overlay);

            // Click outside to dismiss
            overlay.addEventListener('click', function(e) {
                if (e.target === overlay || e.target.classList.contains('spotlight-overlay-bg')) {
                    completeTour();
                }
            });

            // Escape to dismiss
            document.addEventListener('keydown', handleEscape);
        }

        function handleEscape(e) {
            if (e.key === 'Escape') completeTour();
        }

        function showStop(index) {
            var stop = stops[index];
            var el = document.querySelector('[data-spotlight="' + stop.target + '"]');
            if (!el) {
                // Skip this stop if element not found
                if (index < stops.length - 1) { showStop(index + 1); return; }
                else { completeTour(); return; }
            }

            var rect = el.getBoundingClientRect();
            var padding = 8;

            // Position hole
            hole.style.left = (rect.left - padding + window.scrollX) + 'px';
            hole.style.top = (rect.top - padding + window.scrollY) + 'px';
            hole.style.width = (rect.width + padding * 2) + 'px';
            hole.style.height = (rect.height + padding * 2) + 'px';

            // Ensure element is visible
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });

            var contentHtml =
                '<div class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 text-xs font-medium mb-2">' +
                    (window.T?.StepXofY ? window.T.StepXofY.replace('{0}', index + 1).replace('{1}', stops.length) : 'Step ' + (index + 1) + ' of ' + stops.length) +
                '</div>' +
                '<h4 class="font-heading font-bold text-slate-900 dark:text-white mb-1">' + stop.title + '</h4>' +
                '<p class="text-sm text-slate-500 dark:text-slate-400 mb-4">' + stop.description + '</p>' +
                '<div class="flex items-center justify-between">' +
                    '<button type="button" class="spotlight-skip text-sm text-slate-500 hover:text-blue-600 transition-colors">' +
                        (window.T?.SkipTour || 'Skip tour') +
                    '</button>' +
                    '<button type="button" class="spotlight-next px-4 py-1.5 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white rounded-lg text-sm font-medium transition-all">' +
                        (index < stops.length - 1 ? (window.T?.Next || 'Next') : (window.T?.FinishTour || 'Finish')) +
                    '</button>' +
                '</div>';

            isMobile = window.innerWidth < 768;

            if (isMobile) {
                tooltip.classList.remove('visible');
                tooltip.style.display = 'none';
                sheet.innerHTML = contentHtml;
                sheet.style.display = '';
                requestAnimationFrame(function() { sheet.classList.add('visible'); });
            } else {
                sheet.classList.remove('visible');
                sheet.style.display = 'none';
                tooltip.innerHTML = contentHtml;
                tooltip.style.display = '';

                // Position tooltip relative to element
                var tooltipRect;
                tooltip.classList.remove('visible', 'arrow-top', 'arrow-bottom', 'arrow-left', 'arrow-right');

                // Default: below the element
                var top = rect.bottom + padding + 12 + window.scrollY;
                var left = rect.left + window.scrollX;

                // If not enough room below, show above
                if (top + 200 > window.innerHeight + window.scrollY) {
                    top = rect.top - padding - 12 - 180 + window.scrollY;
                    tooltip.classList.add('arrow-bottom');
                } else {
                    tooltip.classList.add('arrow-top');
                }

                // Clamp horizontally
                if (left + 320 > window.innerWidth) {
                    left = window.innerWidth - 330;
                }
                if (left < 10) left = 10;

                tooltip.style.top = top + 'px';
                tooltip.style.left = left + 'px';

                requestAnimationFrame(function() { tooltip.classList.add('visible'); });
            }

            // Bind buttons
            var container = isMobile ? sheet : tooltip;
            container.querySelector('.spotlight-skip').addEventListener('click', completeTour);
            container.querySelector('.spotlight-next').addEventListener('click', function() {
                if (index < stops.length - 1) {
                    currentStop = index + 1;
                    showStop(currentStop);
                } else {
                    completeTour(true);
                }
            });
        }

        function completeTour(withConfetti) {
            if (withConfetti) showConfetti();

            // Fade out
            if (overlay) {
                overlay.style.transition = 'opacity 400ms ease';
                overlay.style.opacity = '0';
                setTimeout(function() {
                    if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
                }, 400);
            }

            document.removeEventListener('keydown', handleEscape);

            // Persist
            localStorage.setItem('rentmate_spotlight_completed', 'true');

            // Sync to server
            fetch('/Onboarding/MarkSpotlightComplete', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken ? getAntiForgeryToken() : ''
                }
            }).catch(function() { /* silent */ });
        }

        function showConfetti() {
            var container = document.createElement('div');
            container.className = 'confetti-container';
            document.body.appendChild(container);

            var colors = ['#2563eb', '#22c55e', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'];
            for (var i = 0; i < 40; i++) {
                var piece = document.createElement('div');
                piece.className = 'confetti-piece';
                piece.style.backgroundColor = colors[Math.floor(Math.random() * colors.length)];
                piece.style.setProperty('--x-start', (Math.random() * 20 - 10) + 'px');
                piece.style.setProperty('--y-start', '0px');
                piece.style.setProperty('--x-end', (Math.random() * 300 - 150) + 'px');
                piece.style.setProperty('--y-end', (Math.random() * 200 + 100) + 'px');
                piece.style.setProperty('--rotation', (Math.random() * 720 - 360) + 'deg');
                piece.style.setProperty('--duration', (Math.random() * 600 + 600) + 'ms');
                piece.style.setProperty('--delay', (Math.random() * 200) + 'ms');
                piece.style.borderRadius = Math.random() > 0.5 ? '50%' : '2px';
                container.appendChild(piece);
                requestAnimationFrame(function() { piece.classList.add('animate'); });
            }

            setTimeout(function() {
                if (container.parentNode) container.parentNode.removeChild(container);
            }, 2000);
        }

        // Start the tour
        setTimeout(function() {
            createOverlay();
            showStop(0);
        }, 500);
    };
})();
```

- [ ] **Step 2: Verify file exists**

Run:
```bash
ls -la wwwroot/js/onboarding.js
```

---

## Task 11: Spotlight Tour Integration (NavBar + Layout + Controller)

**Files:**
- Modify: `Views/Shared/_NavBar.cshtml`
- Modify: `Views/Shared/_Layout.cshtml`
- Modify: `Controllers/Mvc/OnboardingController.cs` (add `MarkSpotlightComplete`)

- [ ] **Step 1: Add `data-spotlight` attributes to `_NavBar.cshtml`**

Add `data-spotlight` attributes to these existing elements:

1. **Search bar / Marketplace link** (line 102-103): Add `data-spotlight="search"` to the Marketplace `<a>` tag:
```html
<a asp-controller="Rentals" asp-action="Index" data-spotlight="search" class="px-4 py-2 ...">
```

2. **Dashboard link** (line 107): Add `data-spotlight="dashboard"`:
```html
<a asp-controller="Dashboard" asp-action="UserDashboard" data-spotlight="dashboard" class="hidden md:block px-4 py-2 ...">
```

3. **Notification bell** (line 289): Wrap the partial call in a span with `data-spotlight="notifications"`:
Change:
```html
@await Html.PartialAsync("_NotificationBell")
```
To:
```html
<span data-spotlight="notifications">@await Html.PartialAsync("_NotificationBell")</span>
```

4. **User menu button** (line 293): Add `data-spotlight="profile"` to the user menu `<div>`:
```html
<div class="relative" data-spotlight="profile">
```

5. **"Become a Host" / List Item button** (line 334): Add `data-spotlight="list-item"`:
```html
<button onclick="..." data-spotlight="list-item" class="inline-flex items-center ...">
```

Note: Only the `data-spotlight="list-item"` attribute is on the unauth button. For authenticated users, the "List Item" CTA lives in the homepage hero, not the navbar. The spotlight JS will find whichever element has the attribute.

- [ ] **Step 2: Add spotlight initialization to `_Layout.cshtml`**

Before the closing `</body>` tag (before line 127 `@await RenderSectionAsync("Scripts"...)`), add:

```html
@if (TempData["ShowSpotlightTour"] != null)
{
    <link rel="stylesheet" href="~/css/onboarding.css" asp-append-version="true" />
    <script src="~/js/onboarding.js" asp-append-version="true"></script>
    <script>
        document.addEventListener('DOMContentLoaded', function() {
            var intent = '@TempData["SpotlightIntent"]';
            var stops;
            if (intent === 'Renter') {
                stops = [
                    { target: 'search', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Search for anything"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightSearchDesc"].Value)) },
                    { target: 'dashboard', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Your dashboard"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightDashboardRenterDesc"].Value)) },
                    { target: 'notifications', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Stay updated"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightNotificationsRenterDesc"].Value)) },
                    { target: 'profile', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Your account"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightProfileDesc"].Value)) }
                ];
            } else if (intent === 'Lister') {
                stops = [
                    { target: 'list-item', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Create a listing"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightListItemDesc"].Value)) },
                    { target: 'dashboard', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Your dashboard"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightDashboardListerDesc"].Value)) },
                    { target: 'notifications', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Stay updated"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightNotificationsListerDesc"].Value)) },
                    { target: 'profile', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Your account"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightProfileDesc"].Value)) }
                ];
            } else {
                stops = [
                    { target: 'search', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Search for anything"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightSearchDesc"].Value)) },
                    { target: 'list-item', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Create a listing"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightListItemDesc"].Value)) },
                    { target: 'dashboard', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Your dashboard"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightDashboardBothDesc"].Value)) },
                    { target: 'notifications', title: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["Stay updated"].Value)), description: @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Localizer["SpotlightNotificationsBothDesc"].Value)) }
                ];
            }
            window.initSpotlightTour({ stops: stops });
        });
    </script>
}
```

- [ ] **Step 3: Add `MarkSpotlightComplete` action to `OnboardingController.cs`**

Add this action inside the `#region Complete Onboarding` region, after the `CompleteOnboarding` action:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> MarkSpotlightComplete()
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Unauthorized();

    user.SpotlightTourCompleted = true;
    await _userManager.UpdateAsync(user);

    return Ok();
}
```

- [ ] **Step 4: Build verification**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeds.

---

## Task 12: Image Brief Placeholders

**Files:**
- Create: `wwwroot/images/onboarding/` directory + 18 text files

- [ ] **Step 1: Create directory and all 18 brief files**

Run:
```bash
mkdir -p wwwroot/images/onboarding
```

Then create each file with its brief content. All files go in `wwwroot/images/onboarding/`:

**welcome-hero-desktop.txt:**
```
Illustration of a friendly neighborhood scene with people exchanging items.
Show a diverse group of 3-4 people, one handing a camera to another, another holding a bicycle.
Warm, inviting colors (blues and greens). Simple flat illustration style.
Desktop size: 560x400px, landscape orientation.
```

**welcome-hero-mobile.txt:**
```
Same neighborhood scene, cropped tighter on 2 people exchanging an item.
Simpler composition for smaller viewport.
Mobile size: 320x240px, landscape orientation.
```

**tour-renter-browse-desktop.txt:**
```
Person browsing items on a phone/laptop screen showing a grid of rental listings.
Include visible category filters and a search bar. Items shown: camera, tent, drill.
Blue accent color. Desktop size: 480x200px.
```

**tour-renter-browse-mobile.txt:**
```
Same concept, simplified. Person with phone showing listing grid.
Mobile size: 320x180px.
```

**tour-renter-rent-desktop.txt:**
```
Calendar interface with date range selected, price breakdown visible.
Show a "Request Rental" button. Clean UI style matching RentMate.
Blue accent color. Desktop size: 480x200px.
```

**tour-renter-rent-mobile.txt:**
```
Simplified calendar with dates selected and price.
Mobile size: 320x180px.
```

**tour-renter-deposit-desktop.txt:**
```
Shield icon with a lock, money/coins being held safely.
Visual metaphor: protection. Show two hands (renter and owner) with a shield between them.
Blue accent color. Desktop size: 480x200px.
```

**tour-renter-deposit-mobile.txt:**
```
Shield with lock, simplified composition.
Mobile size: 320x180px.
```

**tour-renter-reviews-desktop.txt:**
```
Star ratings and review cards. Show 5-star rating with snippet of a positive review.
Include small avatars of reviewers. Trust/community feeling.
Blue accent color. Desktop size: 480x200px.
```

**tour-renter-reviews-mobile.txt:**
```
Star rating with one review card, simplified.
Mobile size: 320x180px.
```

**tour-lister-create-desktop.txt:**
```
Phone/screen showing a "Create Listing" form with photo upload area.
Show a camera icon, title field, and price field. Item photos being added.
Green accent color. Desktop size: 480x200px.
```

**tour-lister-create-mobile.txt:**
```
Simplified create listing screen with photo and title.
Mobile size: 320x180px.
```

**tour-lister-manage-desktop.txt:**
```
Dashboard view showing incoming rental requests as cards.
Show accept/decline buttons on a request card. Notification badge.
Green accent color. Desktop size: 480x200px.
```

**tour-lister-manage-mobile.txt:**
```
Single request card with accept/decline buttons.
Mobile size: 320x180px.
```

**tour-lister-earn-desktop.txt:**
```
Earnings chart/graph trending upward. Show a wallet or bank icon.
Include a payout summary card with a dollar/euro amount.
Green accent color. Desktop size: 480x200px.
```

**tour-lister-earn-mobile.txt:**
```
Simplified earnings with amount and upward arrow.
Mobile size: 320x180px.
```

**tour-lister-reviews-desktop.txt:**
```
Trophy or badge icon with stars. Show a trust score meter.
Include a review card with high rating. Growth/reputation visual.
Green accent color. Desktop size: 480x200px.
```

**tour-lister-reviews-mobile.txt:**
```
Trophy with stars, simplified.
Mobile size: 320x180px.
```

- [ ] **Step 2: Verify all 18 files exist**

Run:
```bash
ls wwwroot/images/onboarding/ | wc -l
```

Expected: 18

---

## Task 13: Localization Keys

**Files:**
- Modify: `Resources/en.json`
- Modify: `Resources/sl.json`

- [ ] **Step 1: Add English localization keys**

Add these keys to `Resources/en.json` in alphabetical order within the existing file. Use a node script to merge them:

```bash
node -e "
const fs = require('fs');
const f = 'Resources/en.json';
const data = JSON.parse(fs.readFileSync(f, 'utf8'));
const newKeys = {
    'About you': 'About you',
    'Add a profile photo': 'Add a profile photo',
    'App Tour': 'App Tour',
    'Back': 'Back',
    'Both': 'Both',
    'Browse items near you': 'Browse items near you',
    'Build your reputation': 'Build your reputation',
    'Change photo': 'Change photo',
    'Continue': 'Continue',
    'Create a listing': 'Create a listing',
    'Deposits protect both sides. They\\'re held safely and released automatically when the rental ends without issues.': 'Deposits protect both sides. They\\'re held safely and released automatically when the rental ends without issues.',
    'Drop it here!': 'Drop it here!',
    'Earn money': 'Earn money',
    'Explore RentMate': 'Explore RentMate',
    'Find and rent items from people near you': 'Find and rent items from people near you',
    'Earn money by sharing your stuff': 'Earn money by sharing your stuff',
    'Find what you need': 'Find what you need',
    'Finish': 'Finish',
    'Finish tour': 'Finish tour',
    'Get paid for each rental. Track your earnings and payouts from your dashboard.': 'Get paid for each rental. Track your earnings and payouts from your dashboard.',
    'Great reviews attract more renters. Respond quickly and keep items in good shape to grow your trust score.': 'Great reviews attract more renters. Respond quickly and keep items in good shape to grow your trust score.',
    'I want to list': 'I want to list',
    'I want to rent': 'I want to rent',
    'Join {0} other members in {1}': 'Join {0} other members in {1}',
    'Join {0} other members on RentMate': 'Join {0} other members on RentMate',
    'LISTING YOUR ITEMS': 'LISTING YOUR ITEMS',
    'List your first item': 'List your first item',
    'Log out': 'Log out',
    'Looking good!': 'Looking good!',
    'Manage requests': 'Manage requests',
    'Max {0}MB': 'Max {0}MB',
    'Next': 'Next',
    'Now let\\'s look at the other side: listing your items and earning money.': 'Now let\\'s look at the other side: listing your items and earning money.',
    'Optional': 'Optional',
    'People are more likely to rent from someone they can see.': 'People are more likely to rent from someone they can see.',
    'Pick the area closest to you. This is used to recommend items nearby and show your approximate area to other users. Your exact address is never shared.': 'Pick the area closest to you. This is used to recommend items nearby and show your approximate area to other users. Your exact address is never shared.',
    'Profile Photo': 'Profile Photo',
    'Profiles with photos get 3x more responses': 'Profiles with photos get 3x more responses',
    'RENTING ITEMS': 'RENTING ITEMS',
    'Rent and list, best of both worlds': 'Rent and list, best of both worlds',
    'Rent anything from people around you, or earn by sharing what you own.': 'Rent anything from people around you, or earn by sharing what you own.',
    'Rent with confidence': 'Rent with confidence',
    'Pick your dates, review the price breakdown, and send a rental request. The owner confirms and you\\'re all set.': 'Pick your dates, review the price breakdown, and send a rental request. The owner confirms and you\\'re all set.',
    'Review rental requests from your dashboard. Accept, decline, or suggest different dates.': 'Review rental requests from your dashboard. Accept, decline, or suggest different dates.',
    'Reviews build trust': 'Reviews build trust',
    'After each rental, both sides leave a review. Verified reviews help the community stay trustworthy.': 'After each rental, both sides leave a review. Verified reviews help the community stay trustworthy.',
    'Search by category, location, or keyword. Filter by price, distance, and availability to find exactly what you\\'re looking for.': 'Search by category, location, or keyword. Filter by price, distance, and availability to find exactly what you\\'re looking for.',
    'Add photos, set your price, and describe your item. It only takes a few minutes to go live.': 'Add photos, set your price, and describe your item. It only takes a few minutes to go live.',
    'Secure deposits': 'Secure deposits',
    'Share location': 'Share location',
    'Show me': 'Show me',
    'Skip for now': 'Skip for now',
    'Skip tour': 'Skip tour',
    'SpotlightDashboardBothDesc': 'Your dashboard is where you manage all your rentals and listings, track requests, and handle deposits.',
    'SpotlightDashboardListerDesc': 'Your dashboard shows incoming rental requests, active rentals, and your earnings overview.',
    'SpotlightDashboardRenterDesc': 'Your dashboard is where you manage all your rentals, track requests, and handle deposits.',
    'SpotlightListItemDesc': 'Start here to create your first listing. Add photos, set a price, and go live in minutes.',
    'SpotlightNotificationsBothDesc': 'You\\'ll get notified here when someone wants to rent your items, responds to your requests, or leaves a review.',
    'SpotlightNotificationsListerDesc': 'You\\'ll get notified here when someone wants to rent your items or leaves a review.',
    'SpotlightNotificationsRenterDesc': 'You\\'ll get notified here when someone responds to your rental request or when there\\'s activity on your account.',
    'SpotlightProfileDesc': 'Access your profile, settings, and account options here. You can update your photo and bio anytime.',
    'SpotlightSearchDesc': 'Type a keyword, category, or item name to find what you need. Results are sorted by distance from your location.',
    'Search for anything': 'Search for anything',
    'State / Region': 'State / Region',
    'Stay updated': 'Stay updated',
    'Step {0} of {1}': 'Step {0} of {1}',
    'Tap to upload': 'Tap to upload',
    'Tell others a bit about yourself...': 'Tell others a bit about yourself...',
    'Tell us about yourself': 'Tell us about yourself',
    'That\\'s how renting works!': 'That\\'s how renting works!',
    'This helps build trust with other members.': 'This helps build trust with other members.',
    'Welcome': 'Welcome',
    'Welcome to RentMate': 'Welcome to RentMate',
    'What brings you here?': 'What brings you here?',
    'Without a location, you\\'ll still be able to browse and use RentMate, but items won\\'t be sorted by distance and your profile won\\'t show an area. You can always add this later in settings.': 'Without a location, you\\'ll still be able to browse and use RentMate, but items won\\'t be sorted by distance and your profile won\\'t show an area. You can always add this later in settings.',
    'You\\'re all set, {0}!': 'You\\'re all set, {0}!',
    'Your account': 'Your account',
    'Your dashboard': 'Your dashboard',
    'or click to browse': 'or click to browse',
    '-- Select a region --': '-- Select a region --',
    'Drag & drop': 'Drag & drop',
    'Create a listing': 'Create a listing'
};
Object.assign(data, newKeys);
const sorted = {};
Object.keys(data).sort((a, b) => a.localeCompare(b)).forEach(k => sorted[k] = data[k]);
fs.writeFileSync(f, JSON.stringify(sorted, null, 2) + '\\n');
console.log('en.json updated: ' + Object.keys(sorted).length + ' keys');
"
```

- [ ] **Step 2: Add Slovenian localization keys**

Same approach for `Resources/sl.json`. Slovenian translations for each key:

```bash
node -e "
const fs = require('fs');
const f = 'Resources/sl.json';
const data = JSON.parse(fs.readFileSync(f, 'utf8'));
const newKeys = {
    'About you': 'O tebi',
    'Add a profile photo': 'Dodaj profilno sliko',
    'App Tour': 'Ogled aplikacije',
    'Back': 'Nazaj',
    'Both': 'Oboje',
    'Browse items near you': 'Razišči predmete v bližini',
    'Build your reputation': 'Zgradi svoj ugled',
    'Change photo': 'Zamenjaj sliko',
    'Continue': 'Nadaljuj',
    'Create a listing': 'Ustvari oglas',
    'Deposits protect both sides. They\\'re held safely and released automatically when the rental ends without issues.': 'Varščine ščitijo obe strani. Varno se hranijo in samodejno sprostijo, ko se najem zaključi brez težav.',
    'Drop it here!': 'Spusti tukaj!',
    'Earn money': 'Zasluži denar',
    'Explore RentMate': 'Razišči RentMate',
    'Find and rent items from people near you': 'Najdi in najemi predmete od ljudi v bližini',
    'Earn money by sharing your stuff': 'Zasluži denar z deljanjem svojih stvari',
    'Find what you need': 'Najdi, kar potrebuješ',
    'Finish': 'Končaj',
    'Finish tour': 'Končaj ogled',
    'Get paid for each rental. Track your earnings and payouts from your dashboard.': 'Prejmi plačilo za vsak najem. Spremljaj zaslužek in izplačila na nadzorni plošči.',
    'Great reviews attract more renters. Respond quickly and keep items in good shape to grow your trust score.': 'Dobre ocene privabijo več najemnikov. Odgovarjaj hitro in vzdržuj predmete v dobrem stanju za višjo oceno zaupanja.',
    'I want to list': 'Želim oddajati',
    'I want to rent': 'Želim najeti',
    'Join {0} other members in {1}': 'Pridruži se {0} drugim članom v kraju {1}',
    'Join {0} other members on RentMate': 'Pridruži se {0} drugim članom na RentMate',
    'LISTING YOUR ITEMS': 'ODDAJANJE PREDMETOV',
    'List your first item': 'Oddaj svoj prvi predmet',
    'Log out': 'Odjava',
    'Looking good!': 'Super izgleda!',
    'Manage requests': 'Upravljaj zahtevke',
    'Max {0}MB': 'Največ {0}MB',
    'Next': 'Naprej',
    'Now let\\'s look at the other side: listing your items and earning money.': 'Zdaj pa poglejmo še drugo stran: oddajanje predmetov in zaslužek.',
    'Optional': 'Neobvezno',
    'People are more likely to rent from someone they can see.': 'Ljudje raje najamejo od nekoga, ki ga lahko vidijo.',
    'Pick the area closest to you. This is used to recommend items nearby and show your approximate area to other users. Your exact address is never shared.': 'Izberi območje, ki ti je najbližje. To se uporablja za priporočanje bližnjih predmetov in prikaz tvojega približnega območja drugim uporabnikom. Tvoj natančen naslov ni nikoli deljen.',
    'Profile Photo': 'Profilna slika',
    'Profiles with photos get 3x more responses': 'Profili s sliko dobijo 3x več odzivov',
    'RENTING ITEMS': 'NAJEMANJE PREDMETOV',
    'Rent and list, best of both worlds': 'Najemi in oddajaj, najboljše iz obeh svetov',
    'Rent anything from people around you, or earn by sharing what you own.': 'Najemi karkoli od ljudi v bližini ali zasluži z deljanjem svojih stvari.',
    'Rent with confidence': 'Najemi z zaupanjem',
    'Pick your dates, review the price breakdown, and send a rental request. The owner confirms and you\\'re all set.': 'Izberi datume, preglej razčlenitev cene in pošlji zahtevek za najem. Lastnik potrdi in vse je pripravljeno.',
    'Review rental requests from your dashboard. Accept, decline, or suggest different dates.': 'Preglej zahtevke za najem na nadzorni plošči. Sprejmi, zavrni ali predlagaj druge datume.',
    'Reviews build trust': 'Ocene gradijo zaupanje',
    'After each rental, both sides leave a review. Verified reviews help the community stay trustworthy.': 'Po vsakem najemu obe strani pustita oceno. Preverjene ocene pomagajo skupnosti ostati zaupanja vredna.',
    'Search by category, location, or keyword. Filter by price, distance, and availability to find exactly what you\\'re looking for.': 'Išči po kategoriji, lokaciji ali ključni besedi. Filtriraj po ceni, razdalji in razpoložljivosti, da najdeš točno to, kar iščeš.',
    'Add photos, set your price, and describe your item. It only takes a few minutes to go live.': 'Dodaj slike, določi ceno in opiši svoj predmet. V nekaj minutah je vse pripravljeno.',
    'Secure deposits': 'Varne varščine',
    'Share location': 'Deli lokacijo',
    'Show me': 'Pokaži mi',
    'Skip for now': 'Preskoči zaenkrat',
    'Skip tour': 'Preskoči ogled',
    'SpotlightDashboardBothDesc': 'Na nadzorni plošči upravljaš vse najeme in oglase, spremljaš zahtevke in urejuješ varščine.',
    'SpotlightDashboardListerDesc': 'Na nadzorni plošči vidiš dohodne zahtevke za najem, aktivne najeme in pregled zaslužka.',
    'SpotlightDashboardRenterDesc': 'Na nadzorni plošči upravljaš vse najeme, spremljaš zahtevke in urejuješ varščine.',
    'SpotlightListItemDesc': 'Tukaj ustvariš svoj prvi oglas. Dodaj slike, določi ceno in objavi v minutah.',
    'SpotlightNotificationsBothDesc': 'Tukaj dobiš obvestila, ko nekdo želi najeti tvoje predmete, odgovori na tvoj zahtevek ali pusti oceno.',
    'SpotlightNotificationsListerDesc': 'Tukaj dobiš obvestila, ko nekdo želi najeti tvoje predmete ali pusti oceno.',
    'SpotlightNotificationsRenterDesc': 'Tukaj dobiš obvestila, ko nekdo odgovori na tvoj zahtevek za najem ali ko se na tvojem računu kaj zgodi.',
    'SpotlightProfileDesc': 'Tukaj dostopaš do profila, nastavitev in možnosti računa. Sliko in opis lahko kadarkoli posodobiš.',
    'SpotlightSearchDesc': 'Vpiši ključno besedo, kategorijo ali ime predmeta, da najdeš, kar potrebuješ. Rezultati so razvrščeni po razdalji od tvoje lokacije.',
    'Search for anything': 'Išči karkoli',
    'State / Region': 'Pokrajina / Regija',
    'Stay updated': 'Bodi na tekočem',
    'Step {0} of {1}': 'Korak {0} od {1}',
    'Tap to upload': 'Tapni za nalaganje',
    'Tell others a bit about yourself...': 'Povej drugim nekaj o sebi...',
    'Tell us about yourself': 'Povej nam o sebi',
    'That\\'s how renting works!': 'Tako deluje najemanje!',
    'This helps build trust with other members.': 'To pomaga graditi zaupanje z drugimi člani.',
    'Welcome': 'Dobrodošli',
    'Welcome to RentMate': 'Dobrodošli na RentMate',
    'What brings you here?': 'Kaj te je pripeljalo sem?',
    'Without a location, you\\'ll still be able to browse and use RentMate, but items won\\'t be sorted by distance and your profile won\\'t show an area. You can always add this later in settings.': 'Brez lokacije boš še vedno lahko brskala po RentMate, vendar predmeti ne bodo razvrščeni po razdalji in tvoj profil ne bo prikazal območja. To lahko dodaš kasneje v nastavitvah.',
    'You\\'re all set, {0}!': 'Vse je pripravljeno, {0}!',
    'Your account': 'Tvoj račun',
    'Your dashboard': 'Tvoja nadzorna plošča',
    'or click to browse': 'ali klikni za brskanje',
    '-- Select a region --': '-- Izberi regijo --',
    'Drag & drop': 'Povleci in spusti',
    'Create a listing': 'Ustvari oglas'
};
Object.assign(data, newKeys);
const sorted = {};
Object.keys(data).sort((a, b) => a.localeCompare(b)).forEach(k => sorted[k] = data[k]);
fs.writeFileSync(f, JSON.stringify(sorted, null, 2) + '\\n');
console.log('sl.json updated: ' + Object.keys(sorted).length + ' keys');
"
```

- [ ] **Step 3: Verify key counts match**

Run:
```bash
node -e "const en = Object.keys(JSON.parse(require('fs').readFileSync('Resources/en.json'))).length; const sl = Object.keys(JSON.parse(require('fs').readFileSync('Resources/sl.json'))).length; console.log('en:', en, 'sl:', sl, en === sl ? 'MATCH' : 'MISMATCH');"
```

Expected: `en: XXXX sl: XXXX MATCH`

---

## Task 14: Final Integration + Verification

**Files:**
- Verify: `Areas/Identity/Pages/Account/Register.cshtml.cs` (redirect should already point to Step1)
- Verify: Full build + smoke test checklist

- [ ] **Step 1: Verify Register redirect**

Check that `Areas/Identity/Pages/Account/Register.cshtml.cs` still redirects to `Step1, Onboarding` after registration. Read lines around 165-170 and confirm:
```csharp
return RedirectToAction("Step1", "Onboarding");
```

No changes needed if this already points to Step1.

- [ ] **Step 2: Full build**

Run:
```bash
dotnet build RentMate.sln
```

Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 3: Run the app**

Run:
```bash
dotnet run --project RentMate-Web/RentMate.csproj
```

Navigate to `https://localhost:7280` and test:

**Smoke test checklist:**
1. Register a new account: redirected to Step 1 (not old onboarding)
2. Step 1: see welcome message, 3 intent cards, progress dots
3. Click an intent card: brief animation, auto-advances to Step 2
4. Step 2: floating labels on name fields, location toggle works
5. Fill names, continue to Step 3
6. Step 3: photo upload area visible, bio textarea with char counter
7. Skip or continue to Step 4
8. Step 4: carousel slides match selected intent, dots work, swipe works
9. Final slide: personalized greeting, CTA button
10. Click CTA: redirected to homepage, spotlight tour starts
11. Spotlight: overlay visible, tooltip/sheet appears, sonar ring animates
12. Complete spotlight: confetti plays, overlay dissolves
13. Refresh page: spotlight does not reappear
14. Dark mode: toggle theme, verify all steps look correct in dark mode
15. Mobile: resize to mobile viewport, verify responsive layout
16. Back button: navigate backward through steps, verify data persists
17. Direct URL: type `/Onboarding/Step3` without completing Step 1/2, verify redirect to Step 1
18. Existing user: log in as existing account, verify they go to homepage (not onboarding)

- [ ] **Step 4: Verify no regressions**

Check that existing functionality still works:
- Login/logout
- Dashboard loads
- Navbar displays correctly (data-spotlight attributes don't affect appearance)
- Marketplace page loads

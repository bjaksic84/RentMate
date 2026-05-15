# Test Suite Audit & Hardening — Full Sweep

## Context

RentMate has 252 tests across 26 files (RentMate.Tests/). Service-level unit
tests are high quality. Two structural defects undermine the integration suite,
and the highest-risk business logic (irreversible account/data deletion,
dashboard aggregation, notification dispatch) has zero coverage. This plan
fixes the defects, upgrades shallow assertions to real behavioral checks, and
fills the coverage gaps. No tests are removed (none are dead or skipped).

Test infra reference (reuse, do not reinvent):
- `RentMate.Tests/Helpers/EntityFactory.cs` — entity builders + `CreateFullRentalSetup()`
- `RentMate.Tests/Helpers/TestDbContextFactory.cs` — `Create()` (InMemory), `CreateSqlite()` (for `ExecuteUpdateAsync`)
- `RentMate.Tests/Infrastructure/IntegrationTestBase.cs` — auth, seeding, JSON helpers, `GetDbContext()`
- `RentMate.Tests/Infrastructure/RentMateWebApplicationFactory.cs` — SQLite host, mocked IPaymentService/IFileUploadService/auth

---

## Phase 1 — Fix integration-test DB isolation (structural)

**Problem:** `RentMateWebApplicationFactory` keeps one SQLite `:memory:`
connection open for the factory lifetime; `_dbInitialized` runs
`EnsureCreated` once. With `IClassFixture`, all tests in a class share one DB
with no per-test reset → accumulated rows, order-dependence.

**Fix (chosen approach):** add a per-test reset hook.
- File: `RentMate.Tests/Infrastructure/RentMateWebApplicationFactory.cs`
  - Add `public void ResetDatabase()` → `EnsureDeleted()` + `EnsureCreated()`, reset seed guard.
- File: `RentMate.Tests/Infrastructure/IntegrationTestBase.cs`
  - In constructor (after `Client` created) call `Factory.ResetDatabase()` so each test starts clean. Constructor runs once per test in xUnit, so this gives per-test isolation while keeping the shared connection.
  - Update `ViewSmokeTests_NewEndpoints` to extend `IntegrationTestBase` (see Phase 3) so it inherits the reset.
- Verify each existing integration test still seeds what it needs in its own constructor (they do — seeding is in test-class constructors, which run after base constructor).

---

## Phase 2 — Strengthen shallow assertions

Replace `status < 500` smoke asserts with behavior + DB side-effect checks
using the existing `GetDbContext()` helper.

- `RentMate.Tests/Controllers/ItemsControllerTests.cs`
  - `Create_ValidItem`: assert 302 redirect AND `GetDbContext().Items` contains the new item with expected Title/Price/OwnerId.
  - `Edit_OwnerCanEdit`: assert persisted Title/Price changed in DB.
  - `ToggleListing`: assert `Item.IsListed` flipped in DB, not just "success" substring.
- `RentMate.Tests/Controllers/RentalsControllerTests.cs`, `DashboardControllerTests.cs`: same treatment — assert redirect target / model state / DB rows, not just non-500.
- Keep genuine smoke tests (page-render GETs in `ViewSmokeTests`) as smoke, but rename methods to `..._RendersWithoutServerError` so intent is explicit and they aren't mistaken for behavioral tests.

---

## Phase 3 — Consolidate duplicates + cosmetic cleanup

- Merge `RentMate.Tests/Controllers/ViewSmokeTests_NewEndpoints.cs` into `ViewSmokeTests.cs` as `[Theory]` rows; delete the file and its duplicated auth helpers (use `IntegrationTestBase.AuthenticateAs`).
- `RentMate.Tests/Services/DepositServiceTests.cs`: remove leftover `// STEP 3 / STEP 4 / STEP 5` plan-scaffold banner comments. No logic change.

---

## Phase 4 — Fill high-risk service coverage (new test files)

Pattern: mirror `DepositServiceTests` style — xUnit `[Fact]`, `EntityFactory`
seeding, `ChangeTracker.Clear()`, `TestDbContextFactory.Create()` or
`CreateSqlite()` if the service uses `ExecuteUpdateAsync`.

1. `RentMate.Tests/Services/AccountLifecycleServiceTests.cs` (CRITICAL — irreversible)
   - `HasActiveRentalsAsync` true/false; `DeactivateAccountAsync` sets flags + blocks when active rentals; `ReactivateAccountAsync`; `PermanentlyDeleteAccountAsync` anonymises/cascades correctly and is idempotent/guarded.
2. `RentMate.Tests/Services/DataRetentionServiceTests.cs`
   - Each purge method removes only rows past retention window, leaves recent rows, cascades cleanly. Verify `RunRetentionPassAsync` orchestration.
3. `RentMate.Tests/Services/DashboardServiceTests.cs`
   - `GetUserDashboardAsync` / `GetOwnerRentalsAsync` / `GetMyRentalsAsync` return correct counts/joins for seeded data; `GetAdminDashboardAsync` stats; `InvalidateAdminCache` behavior.
4. `RentMate.Tests/Services/NotificationDispatcherTests.cs`
   - Mock `INotificationService` (or `IHubContext<RentMateHub>` as in `NotificationServiceTests`); assert each event method (`RentalRequestedAsync`, `DepositChargedAsync`, etc.) creates the right notification payload/recipient.

---

## Phase 5 — Untested MVC controllers (integration)

New files extending `IntegrationTestBase`, behavioral asserts (Phase 2 standard):
- `ProfileControllerTests.cs` — view + profile update persistence + auth guard.
- `ReviewsControllerTests.cs` — create review persists, ownership/auth guards, aggregation trigger.
- `NotificationControllerTests.cs` — list/mark-read/dismiss JSON endpoints + DB effect.
- `PaymentControllerTests.cs` — endpoints with mocked `IPaymentService` (already mocked in factory); assert redirect/JSON, not Stripe internals.
- `CurrencyControllerTests.cs` / `CultureControllerTests.cs` — cookie set + redirect.
(Skip API controllers — `Controllers/Api/` is read-only per CLAUDE.md.)

---

## Phase 6 — Domain computed properties + Stripe error mapping

- `RentMate.Tests/Domain/DomainComputedPropertyTests.cs` (pure unit, no DbContext)
  - `Item.PrimaryImageUrl` fallback (ItemImages by DisplayOrder → legacy `ImageUrl`), `Item.IsRented`; `ApplicationUser` scoring/verified flags; `RentalStateExtensions` status transitions.
- `RentMate.Tests/Services/StripePaymentServiceTests.cs` (error-mapping harness only)
  - Inject a fake/mocked Stripe client boundary; assert `StripeException` → `PaymentResult.Failed(...)` mapping and success mapping for Authorize/Capture/Release/Refund. No live API calls. If the service has no seam for injecting the Stripe client, document that a thin wrapper interface is the prerequisite and scope this sub-task as "add seam + test".

---

## Critical files

Modify:
- `RentMate.Tests/Infrastructure/RentMateWebApplicationFactory.cs`
- `RentMate.Tests/Infrastructure/IntegrationTestBase.cs`
- `RentMate.Tests/Controllers/ItemsControllerTests.cs`, `RentalsControllerTests.cs`, `DashboardControllerTests.cs`, `ViewSmokeTests.cs`
- `RentMate.Tests/Services/DepositServiceTests.cs` (comment cleanup only)

Delete:
- `RentMate.Tests/Controllers/ViewSmokeTests_NewEndpoints.cs` (merged into `ViewSmokeTests.cs`)

Create:
- `RentMate.Tests/Services/AccountLifecycleServiceTests.cs`, `DataRetentionServiceTests.cs`, `DashboardServiceTests.cs`, `NotificationDispatcherTests.cs`, `StripePaymentServiceTests.cs`
- `RentMate.Tests/Controllers/ProfileControllerTests.cs`, `ReviewsControllerTests.cs`, `NotificationControllerTests.cs`, `PaymentControllerTests.cs`, `CurrencyControllerTests.cs`, `CultureControllerTests.cs`
- `RentMate.Tests/Domain/DomainComputedPropertyTests.cs`

Read first (to learn signatures before writing tests):
- `RentMate-Web/Services/Implementations/AccountLifecycleService.cs`, `DataRetentionService.cs`, `DashboardService.cs`, `NotificationDispatcher.cs`, `StripePaymentService.cs`
- `RentMate-Web/Controllers/Mvc/ProfileController.cs`, `ReviewsController.cs`, `NotificationController.cs`, `PaymentController.cs`, `CurrencyController.cs`, `CultureController.cs`
- `RentMate-Web/Models/Domain/Item.cs`, `ApplicationUser.cs`, `RentalStateExtensions.cs`

---

## Verification

Per CLAUDE.md "one step at a time": complete one phase per turn, build + test
after each, then proceed.

```bash
dotnet build RentMate.sln
dotnet test RentMate.Tests/RentMate.Tests.csproj
```

End-to-end checks:
1. After Phase 1: run the full integration suite twice and with
   `--filter` reordering — results stable, no order-dependent failures.
2. After Phase 2: temporarily break a controller (e.g. skip DB save) and
   confirm the strengthened test now FAILS (asserts real behavior, not just non-500).
3. After Phases 4–6: new tests green; total count rises from 252; no skips.
4. Final: `dotnet test` all green, build clean, zero warnings introduced.

Note: project memory says plans live in `RentMate-Web/docs/plans/`. Plan mode
restricts edits to this file; on approval, copy this plan there before
implementation.

---

## Known gap (deferred): StripePaymentService error-mapping tests

`StripePaymentService` constructs the Stripe SDK clients inline in every
method (`new PaymentIntentService()`, `new RefundService()`,
`new CustomerService()`, ...) against the global `StripeConfiguration.ApiKey`.
There is no `IStripeClient` injection point, so the `StripeException →
PaymentResult.Failed` / success-mapping logic cannot be unit tested without
hitting the real Stripe API.

Prerequisite to test it: add a seam — either pass an `IStripeClient` into each
`*Service` constructor (Stripe SDK supports this) or wrap the Stripe calls
behind a thin internal interface that can be mocked. That is a production
change to the payment path and is intentionally **out of scope** for this
test-hardening pass. Tracked here so it is not silently forgotten.

Implemented in this pass instead: `PaymentControllerTests` covers the
deterministic `[Authorize]` / not-found guard paths (the factory already
mocks `IPaymentService`), and `DepositServiceTests` exercises the deposit
flow with a mocked `IPaymentService`, so payment *orchestration* is covered
even though the Stripe adapter itself is not.

## Outcome

- Suite: 253 → 301 tests, 0 skipped, deterministic across repeated full runs.
- Phase 1 isolation fix + assembly serialization + factory `IScoringService`
  mock removed all order-dependent / fire-and-forget flakiness.

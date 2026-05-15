# Remove government-ID verification gate blocking listing

> On implementation, copy this plan to `RentMate-Web/docs/plans/` (user preference;
> plan-mode can only write this harness path).

## Context

A "government ID" gate blocks users from listing items: any user with
`ApplicationUser.IsGovernmentIdVerified == false` is redirected away with the
message *"You must verify your government ID before listing items for rent..."*.

The flag `IsGovernmentIdVerified` is **never set to `true` anywhere** — no KYC
flow, no admin action, no profile setting assigns it. Default is `false`, so
**every user is permanently blocked from publishing items** (web and mobile).
User wants this gate removed entirely. User confirmed: remove the API gate too
(overriding the usual Controllers/Api/ read-only rule for this change).

The `IsGovernmentIdVerified` property itself stays — it is still used (display
only, non-blocking) by ScoringService, ProfileCompletionService, owner badges,
dashboard. Only the listing-blocking checks are removed.

## Critical files

- `RentMate-Web/Controllers/Mvc/ItemsController.cs` — 2 gates
- `RentMate-Web/Controllers/Api/ItemsApiController.cs` — 1 gate (user-approved exception to read-only rule)

## Changes

### 1. ItemsController.cs — `Create()` GET (lines 104-110)

Remove the entire gate block (comment + `var user` fetch + `if`). Result:

```csharp
public async Task<IActionResult> Create()
{
    ViewData["UserId"] = new SelectList(_db.Users, "Id", "Email");
    return View();
}
```

(`user` was only used by the gate; removing it avoids an unused local.)

### 2. ItemsController.cs — `ToggleListing()` (lines 349-354)

Remove only the gate block (comment + `if`). Keep `var user` (still used at
line 344 `item.UserId != user.Id`). `item.IsListed` toggle proceeds directly
after the ownership check.

### 3. ItemsApiController.cs — `PostItem()` (lines 149-154)

Remove the entire gate block (comment + `var user` fetch + `if`). The following
code uses `userId` / `newItem.User`, not `user`, so removal is clean.

No other code touched. `IsGovernmentIdVerified` property and all its display
usages remain intact. No DB migration (property kept).

## Verification

1. Build: `dotnet build RentMate.sln -clp:ErrorsOnly` — 0 errors (confirms no unused-variable / leftover-reference breakage).
2. Run: `dotnet run --project RentMate-Web/RentMate.csproj`, browse https://localhost:7280.
3. As a normal (unverified) user: open Create Item form — loads (no redirect/banner). Submit a new item — created as draft.
4. From dashboard, toggle the item to Listed — succeeds, item appears in marketplace, no error banner.
5. Confirm the banner *"You must verify your government ID before listing items for rent..."* no longer appears anywhere.
6. Regression: item owner "ID Verified" badge / profile-completion tip still render (display-only path unaffected).
7. API (mobile): `POST /api/items` as an unverified user returns the created item, not 400 "You must verify your government ID...".

## Rollback

Pure code deletion, no migration. `git checkout` the two controller files.

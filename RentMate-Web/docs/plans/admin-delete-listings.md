# Admin: permanently delete listings

> Note: per user preference, on implementation place a copy of this plan at
> `RentMate-Web/docs/plans/`. Plan-mode could only write this harness path.

## Context

Admins can already **hide/restore** listings (`IsAdminHidden` via `AdminToggleHide`),
but cannot remove a listing outright. The user wants admins to also **permanently
delete** a listing from the AdminDashboard. Keep Hide; add Delete alongside it.

Decisions (from user):
- Keep Hide. Add a **separate permanent hard-delete** (no soft-delete flag, no migration).
- Admin **force-delete**: overrides the active-rentals guard that blocks owner delete.
  Strong confirmation warning in the UI.
- Expose **only** in the AdminDashboard "All Listings" table, next to Hide/Restore.
- MVC only. `Controllers/Api/` untouched (mobile app, read-only).

Hard delete is safe: `RentMateContext` cascades Item -> Rentals/Reviews/Images/
Accessories/Favorites, and Rental -> Deposit/Accessory/Extension -> DisputeEvidence.
No migration needed.

## Critical files

- `RentMate-Web/Controllers/Mvc/ItemsController.cs` — refactor + new action
- `RentMate-Web/Views/Dashboard/AdminDashboard.cshtml` — button + JS
- `RentMate-Web/Resources/en.json`, `RentMate-Web/Resources/sl.json` — 2 new keys

## Step 1 — ItemsController: extract shared helper + add admin action

`RentMate-Web/Controllers/Mvc/ItemsController.cs`

### 1a. Add private helper (insert after `DeleteConfirmed`, after line 324)

Single source of truth for the destructive part. No auth/business guards — callers own those.

```csharp
/// <summary>
/// Permanently deletes an item: wipes its Cloudinary images (incl. the legacy
/// single-image field) then removes the row. EF cascade removes related rentals,
/// reviews, images, accessories and favorites. Caller owns authorization and any
/// business-rule guards. The item MUST be loaded with .Include(i => i.Images).
/// </summary>
private async Task DeleteItemCoreAsync(Item item)
{
    var imageUrls = item.Images.Select(i => i.ImageUrl).ToList();
    if (!string.IsNullOrEmpty(item.ImageUrl) && !imageUrls.Contains(item.ImageUrl))
        imageUrls.Add(item.ImageUrl);
    await _fileUploadService.DeleteFilesAsync(imageUrls);

    _db.Items.Remove(item);
    await _db.SaveChangesAsync();
}
```

### 1b. Refactor `DeleteConfirmed` (lines 313-320) to call helper

Behavior unchanged. Replace lines 313-320 (the Cloudinary block + `_db.Items.Remove`
+ `SaveChanges`) with:

```csharp
                await DeleteItemCoreAsync(item);
```

The owner active-rentals guard (lines 302-311) stays before the call.

### 1c. Add admin force-delete (insert after `AdminToggleHide`, after line 488)

Calls helper directly -> skips active-rentals guard (force). Mirrors `AdminToggleHide`
shape (`Json`, `NotFound`). Reuses already-injected `_logger`.

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = AdminRole)]
public async Task<IActionResult> AdminDeleteItem(int id)
{
    var item = await _db.Items
        .Include(i => i.Images)
        .FirstOrDefaultAsync(i => i.Id == id);
    if (item == null) return NotFound();

    try
    {
        // Admin force-delete: intentionally skips the active-rentals guard.
        // EF cascade removes rentals, reviews, images, accessories, favorites.
        await DeleteItemCoreAsync(item);

        _logger.LogWarning(
            "ADMIN force-deleted item {ItemId} ('{Title}') owned by {OwnerId}. Admin: {AdminId}",
            id, item.Title, item.UserId, _userManager.GetUserId(User));

        return Json(new { success = true });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to admin-delete item {ItemId}", id);
        return Json(new { success = false, message = _localizer["Error deleting listing"].Value });
    }
}
```

No new `using` directives needed.

## Step 2 — AdminDashboard: button + JS

`RentMate-Web/Views/Dashboard/AdminDashboard.cshtml`

### 2a. Delete button (insert in actions cell, after Hide button line 276, before Details `<a>` line 277)

```html
                                    <button class="admin-delete-btn px-3 py-1.5 text-sm font-medium rounded-lg transition-colors bg-red-600 hover:bg-red-700 text-white"
                                            onclick="adminDeleteItem(@item.Id, this)">
                                        <i class="bi bi-trash"></i>
                                        @Localizer["Delete"]
                                    </button>
```

Reuses existing `"Delete"` key.

### 2b. JS function (insert inside `@section Scripts` `<script>`, after `adminToggleHide` close line 325, before `</script>` line 326)

```javascript
async function adminDeleteItem(itemId, button) {
    if (!confirm("@Localizer["Are you sure you want to permanently delete this listing? This will also remove all related rentals, reviews and images. This action cannot be undone."]")) return;

    button.disabled = true;
    try {
        const res = await fetch(`/Items/AdminDeleteItem/${itemId}`, {
            method: "POST",
            headers: {
                "X-CSRF-TOKEN": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
            }
        });
        if (res.ok) {
            const data = await res.json();
            if (data.success) { button.closest("tr").remove(); return; }
            alert(data.message || "@Localizer["Error deleting listing"]");
        } else {
            alert("@Localizer["Error deleting listing"]");
        }
    } catch (err) {
        console.error(err);
        alert("@Localizer["Network error"]");
    }
    button.disabled = false;
}
```

`/Items/AdminDeleteItem/{id}` resolves via default route (same as `AdminToggleHide`).
`"Network error"` key already exists; do not re-add.

## Step 3 — Localization (2 new keys, both files, alphabetical)

Reuse existing `"Delete"` and `"Network error"`. Add only these 2 keys.
Use `node -e` to insert + keep alphabetical (no Python).

`en.json`:
```json
"Are you sure you want to permanently delete this listing? This will also remove all related rentals, reviews and images. This action cannot be undone.": "Are you sure you want to permanently delete this listing? This will also remove all related rentals, reviews and images. This action cannot be undone.",
"Error deleting listing": "Error deleting listing",
```

`sl.json`:
```json
"Are you sure you want to permanently delete this listing? This will also remove all related rentals, reviews and images. This action cannot be undone.": "Ali ste prepričani, da želite trajno izbrisati to objavo? S tem boste odstranili tudi vse povezane najeme, ocene in slike. Tega dejanja ni mogoče razveljaviti.",
"Error deleting listing": "Napaka pri brisanju objave",
```

## Verification

1. Build: `dotnet build ../RentMate.sln` — 0 errors.
2. JSON parse: `node -e "JSON.parse(require('fs').readFileSync('RentMate-Web/Resources/en.json','utf8'));JSON.parse(require('fs').readFileSync('RentMate-Web/Resources/sl.json','utf8'));console.log('ok')"`
3. Run: `dotnet run --project RentMate.csproj`, browse https://localhost:7280.
4. Admin login (`admin@rentmate.com`) -> AdminDashboard -> All Listings: each row shows Hide/Restore + red Delete + Details.
5. Delete a listing **without** active rentals -> strong confirm -> row vanishes, no reload. DB: `SELECT * FROM "Items" WHERE "Id"=<id>;` -> 0 rows; cascades gone (Rentals/Reviews/ItemImages/ItemAccessories/AccountItemFavorites). Cloudinary URLs no longer resolve.
6. Delete a listing **with** an active rental -> confirm -> row vanishes (force-delete overrides guard). Verify rental rows cascaded.
7. Regression: Hide/Restore still works on another listing. Owner Delete on item with active rentals still blocked with existing message.
8. Switch to Slovenian -> confirm dialog shows Slovenian text.
9. Negative: non-admin `POST /Items/AdminDeleteItem/<id>` -> 403/redirect, no delete. Non-existent id -> `NotFound()` (404), row stays.

## Rollback

Pure code/JSON, no migration. Revert the 4 files.

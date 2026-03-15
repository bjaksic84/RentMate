using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Services.Implementations;

/// <summary>
/// Background service that enforces data retention policies.
/// Runs once daily at 03:00 UTC.
/// </summary>
public class DataRetentionService : BackgroundService
{
    private const int RentalRetentionYears = 5;
    private const int DeletedReviewRetentionDays = 365;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data retention service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun();
            _logger.LogInformation("Data retention: next run in {Delay}.", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await RunRetentionPassAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Data retention run failed.");
            }
        }
    }

    private async Task RunRetentionPassAsync(CancellationToken ct)
    {
        _logger.LogInformation("Data retention pass started at {Now:O}.", DateTime.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RentMateContext>();
        var fileUpload = scope.ServiceProvider.GetRequiredService<IFileUploadService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await PurgeExpiredRentalsAsync(context, fileUpload, ct);
        await PurgeAnonymisedUsersAsync(context, userManager, ct);
        await PurgeDeletedReviewsAsync(context, ct);

        _logger.LogInformation("Data retention pass completed at {Now:O}.", DateTime.UtcNow);
    }

    /// <summary>
    /// Hard-deletes completed/cancelled rentals older than <see cref="RentalRetentionYears"/> years.
    /// Cloudinary evidence files are removed before the DB delete.
    /// DB cascade handles: RentalDeposits, DisputeEvidences, RentalExtensions, RentalAccessories, Payments, Reviews.
    /// </summary>
    private async Task PurgeExpiredRentalsAsync(
        RentMateContext context, IFileUploadService fileUpload, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddYears(-RentalRetentionYears);

        var expiredIds = await context.Rentals
            .Where(r =>
                (r.Status == RentalStatus.Completed || r.Status == RentalStatus.Cancelled) &&
                r.CreatedAt < cutoff)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (expiredIds.Count == 0) return;

        // Collect Cloudinary evidence URLs before the DB rows disappear
        var evidenceUrls = await context.DisputeEvidences
            .Where(e => e.RentalDeposit != null &&
                        expiredIds.Contains(e.RentalDeposit.RentalId) &&
                        e.Url != null)
            .Select(e => e.Url!)
            .Distinct()
            .ToListAsync(ct);

        if (evidenceUrls.Count > 0)
        {
            try
            {
                await fileUpload.DeleteFilesAsync(evidenceUrls);
                _logger.LogInformation(
                    "Data retention: deleted {FileCount} Cloudinary evidence files from {RentalCount} expired rentals.",
                    evidenceUrls.Count, expiredIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Data retention: Cloudinary evidence cleanup partially failed.");
            }
        }

        // Load and delete; DB cascade removes deposits, extensions, accessories, payments
        var rentals = await context.Rentals
            .Where(r => expiredIds.Contains(r.Id))
            .ToListAsync(ct);

        context.Rentals.RemoveRange(rentals);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Data retention: purged {Count} rentals created before {Cutoff:yyyy-MM-dd}.",
            expiredIds.Count, cutoff);
    }

    /// <summary>
    /// Hard-deletes anonymised user records (deleted_*@deleted.rentmate) that have no remaining rental history.
    /// These accumulate after <see cref="PurgeExpiredRentalsAsync"/> removes their last rentals.
    /// </summary>
    private async Task PurgeAnonymisedUsersAsync(
        RentMateContext context, UserManager<ApplicationUser> userManager, CancellationToken ct)
    {
        var anonymised = await context.Users
            .Where(u => u.Email != null &&
                        u.Email.StartsWith(AccountLifecycleService.AnonymisedEmailPrefix) &&
                        u.Email.EndsWith(AccountLifecycleService.AnonymisedEmailSuffix))
            .ToListAsync(ct);

        if (anonymised.Count == 0) return;

        int purged = 0;
        foreach (var user in anonymised)
        {
            var hasRentals = await context.Rentals
                .AnyAsync(r => r.RenterId == user.Id || r.OwnerId == user.Id, ct);

            if (hasRentals) continue;

            var result = await userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                purged++;
            }
            else
            {
                _logger.LogWarning(
                    "Data retention: failed to delete anonymised user {UserId}: {Errors}",
                    user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (purged > 0)
            _logger.LogInformation(
                "Data retention: purged {Count} anonymised user records with no remaining rental history.", purged);
    }

    /// <summary>
    /// Hard-deletes soft-deleted reviews older than <see cref="DeletedReviewRetentionDays"/> days.
    /// </summary>
    private async Task PurgeDeletedReviewsAsync(RentMateContext context, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-DeletedReviewRetentionDays);

        var reviews = await context.Reviews
            .Where(r => r.IsDeleted && (r.UpdatedAt ?? r.CreatedAt) < cutoff)
            .ToListAsync(ct);

        if (reviews.Count == 0) return;

        context.Reviews.RemoveRange(reviews);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Data retention: purged {Count} soft-deleted reviews older than {Cutoff:yyyy-MM-dd}.",
            reviews.Count, cutoff);
    }

    /// <summary>Returns the time to wait until the next 03:00 UTC run.</summary>
    private static TimeSpan DelayUntilNextRun()
    {
        var now = DateTime.UtcNow;
        var nextRun = now.Date.AddHours(3); // 03:00 UTC today
        if (now >= nextRun) nextRun = nextRun.AddDays(1); // already past → tomorrow
        return nextRun - now;
    }
}

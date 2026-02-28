using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations;

/// <summary>
/// Background service that periodically recomputes all marketplace ranking scores.
/// 
/// Per §9 of the ranking design doc:
/// - Event-driven scores update immediately (handled by controller hooks).
/// - Relative/batch scores (pricing competitiveness, demand, global mean,
///   view-count decay, profile trust scores, item scores) are refreshed
///   every 4–6 hours via this service.
/// </summary>
public class ScoringBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScoringBackgroundService> _logger;

    /// <summary>Batch recalculation interval (4 hours).</summary>
    private static readonly TimeSpan RecalcInterval = TimeSpan.FromHours(4);

    public ScoringBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScoringBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scoring background service started. Recalc interval: {Interval}", RecalcInterval);

        // Initial delay to let the app finish startup
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting scheduled scoring recalculation");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                using var scope = _scopeFactory.CreateScope();
                var scoringService = scope.ServiceProvider.GetRequiredService<IScoringService>();

                await scoringService.RunFullRecalculationAsync(stoppingToken);

                sw.Stop();
                _logger.LogInformation("Scoring recalculation completed in {Elapsed:F1}s", sw.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Scoring background service shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled scoring recalculation");
            }

            await Task.Delay(RecalcInterval, stoppingToken);
        }
    }
}

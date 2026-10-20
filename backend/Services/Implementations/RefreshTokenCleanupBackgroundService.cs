using backend.Data;

namespace backend.Services.Implementations;

public sealed class RefreshTokenCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RefreshTokenCleanupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private const int DefaultIntervalHours = 6;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = GetPositiveInt("RefreshTokenCleanup:IntervalHours", DefaultIntervalHours);
        var retentionDays = GetNonNegativeInt(
            "RefreshTokenCleanup:RetentionDays", RefreshTokenCleaner.DefaultRetentionDays);

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<MtsDbContext>();
                    var deleted = await RefreshTokenCleaner.CleanupAsync(
                        context, DateTime.UtcNow, retentionDays, ct: stoppingToken);

                    if (deleted > 0)
                    {
                        logger.LogInformation(
                            "Deleted {DeletedCount} expired refresh tokens older than {RetentionDays} day(s).",
                            deleted, retentionDays);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Expired refresh token cleanup failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private int GetPositiveInt(string key, int fallback) =>
        int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;

    private int GetNonNegativeInt(string key, int fallback) =>
        int.TryParse(configuration[key], out var value) && value >= 0 ? value : fallback;
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Edith.Screening.Bulk.Feeds;

public sealed partial class BlacklistSyncWorker : BackgroundService
{
    private readonly IBlacklistSyncService _syncService;
    private readonly ILogger<BlacklistSyncWorker> _logger;

    public BlacklistSyncWorker(
        IBlacklistSyncService syncService,
        ILogger<BlacklistSyncWorker> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "BlacklistSyncWorker started (internal UTC scheduler).")]
    private static partial void LogSchedulerStarted(ILogger logger);

    [LoggerMessage(EventId = 102, Level = LogLevel.Information, Message = "Triggering scheduled daily blacklist update at {ScheduledTimeUtc} UTC.")]
    private static partial void LogScheduledTrigger(ILogger logger, string scheduledTimeUtc);

    [LoggerMessage(EventId = 103, Level = LogLevel.Error, Message = "Error in BlacklistSyncWorker schedule check loop.")]
    private static partial void LogSchedulerError(ILogger logger, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogSchedulerStarted(_logger);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var config = await _syncService.GetScheduleConfigAsync(stoppingToken);
                    if (!config.IsScheduleEnabled)
                    {
                        continue;
                    }

                    if (!TimeOnly.TryParse(config.DailyExecutionTimeUtc, out var targetTime))
                    {
                        targetTime = new TimeOnly(2, 0);
                    }

                    var nowUtc = DateTime.UtcNow;
                    var todayUtc = DateOnly.FromDateTime(nowUtc);
                    var currentTimeUtc = TimeOnly.FromDateTime(nowUtc);

                    if (config.LastScheduledRunDate.HasValue && config.LastScheduledRunDate.Value >= todayUtc)
                    {
                        continue;
                    }

                    // Trigger when current UTC time reaches or passes the configured daily execution time within a 15-minute window
                    if (currentTimeUtc >= targetTime && currentTimeUtc <= targetTime.AddMinutes(15))
                    {
                        LogScheduledTrigger(_logger, config.DailyExecutionTimeUtc);
                        await _syncService.TriggerSyncAsync(
                            triggerMode: "SCHEDULED",
                            triggeredBy: $"Internal Scheduler ({config.DailyExecutionTimeUtc} UTC)",
                            selectedSources: null,
                            waitForCompletion: true,
                            ct: stoppingToken);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogSchedulerError(_logger, ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
    }
}

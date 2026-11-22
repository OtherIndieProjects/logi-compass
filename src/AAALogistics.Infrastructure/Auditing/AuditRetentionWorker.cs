using AAALogistics.Application.Logistics;
using AAALogistics.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Auditing;

/// <summary>
/// Periodic retention/checkpoint job. Never deletes audit rows without a covering
/// checkpoint (append-only chain); creates daily checkpoints when enabled.
/// </summary>
public sealed class AuditRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditOptions> options,
    ILogger<AuditRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RetentionJobEnabled)
        {
            logger.LogInformation("Audit retention job disabled.");
            return;
        }

        var pollHours = Math.Clamp(options.Value.RetentionPollHours, 1, 168);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
                var status = await audit.GetRetentionStatusAsync(stoppingToken);
                if (status.LatestCheckpointSequence is null
                    || status.EntriesOlderThanRetention > 0)
                {
                    var checkpoint = await audit.CreateCheckpointAsync(
                        "retention-job",
                        stoppingToken);
                    logger.LogInformation(
                        "Audit checkpoint created through sequence {Sequence} at {Path}.",
                        checkpoint.ThroughSequence,
                        checkpoint.ExternalPath);
                    status = await audit.GetRetentionStatusAsync(stoppingToken);
                }

                if (status.PurgeBlockedWithoutCheckpoint)
                {
                    logger.LogWarning(
                        "Audit retention: purge blocked without checkpoint. {Message}",
                        status.Message);
                }
                else
                {
                    logger.LogInformation(
                        "Audit retention status: {Message}",
                        status.Message);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Audit retention worker cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(pollHours), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

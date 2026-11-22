using AAALogistics.Application.Logistics;
using AAALogistics.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Logistics;

public sealed class SapOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SapOptions> options,
    ILogger<SapOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = Math.Clamp(options.Value.OutboxPollSeconds, 5, 300);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<ISapIntegrationService>();
                await service.ProcessOutboxAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "SAP outbox worker cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(poll), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

using FxLink.Abstractions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FxLink.BackgroundServices;

// Retention half of the Outbox pattern. Deletes DeadLettered rows older than RetentionPeriod so
// stores don't grow forever — Dispatched rows never reach this worker at all, since
// IOutboxStore.MarkDispatchedAsync removes them the moment they dispatch successfully. Unlike
// OutboxDispatcherWorker, this doesn't need IPartitionLeaseStore coordination —
// DeleteDispatchedBeforeAsync only ever touches terminal rows, so multiple instances running
// their own cleanup tick concurrently is naturally safe (deleting an already-deleted row is a
// no-op, there's no ordering concern for rows nothing reads anymore).
//
// Each tick opens its own scope and re-resolves IOutboxStore from it — same reasoning as
// OutboxDispatcherWorker: a scope-aware backend can't be held across ticks by this Singleton service.
internal sealed class OutboxCleanupWorker(
    IOutboxRegistry registry,
    IServiceProvider serviceProvider,
    ILogger<OutboxCleanupWorker> logger,
    IOutboxDispatcherOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hasDefault = registry.HasDefault;
        var keyedMessageTypes = registry.KeyedMessageTypes.ToList();
        if (!hasDefault && keyedMessageTypes.Count == 0) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var cutoff = DateTime.UtcNow - options.RetentionPeriod;

                if (hasDefault)
                    await scope.ServiceProvider.GetRequiredService<IOutboxStore>()
                        .DeleteDispatchedBeforeAsync(cutoff, stoppingToken);

                foreach (var messageType in keyedMessageTypes)
                    await scope.ServiceProvider.GetRequiredKeyedService<IOutboxStore>(messageType)
                        .DeleteDispatchedBeforeAsync(cutoff, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox cleanup tick failed");
            }

            try
            {
                await Task.Delay(options.CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
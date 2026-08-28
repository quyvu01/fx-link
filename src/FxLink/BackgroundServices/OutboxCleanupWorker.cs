using FxLink.Registries;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FxLink.BackgroundServices;

// Retention half of the Outbox pattern. Deletes Dispatched/DeadLettered rows older than
// RetentionPeriod so stores don't grow forever. Unlike OutboxDispatcherWorker, this doesn't need
// IPartitionLeaseStore coordination — DeleteDispatchedBeforeAsync only ever touches terminal rows,
// so multiple instances running their own cleanup tick concurrently is naturally safe (deleting an
// already-deleted row is a no-op, there's no ordering concern for rows nothing reads anymore).
internal sealed class OutboxCleanupWorker(
    IOutboxRegistry registry,
    IServiceProvider serviceProvider,
    ILogger<OutboxCleanupWorker> logger,
    IOutboxDispatcherOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stores = registry.ResolveOutboxStores(serviceProvider).ToList();
        if (stores.Count == 0) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cutoff = DateTime.UtcNow - options.RetentionPeriod;
                foreach (var store in stores)
                    await store.DeleteDispatchedBeforeAsync(cutoff, stoppingToken);
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

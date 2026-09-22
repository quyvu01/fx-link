using FxLink.Abstractions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FxLink.BackgroundServices;

// Retention half of the Inbox pattern — the counterpart to OutboxCleanupWorker. Deletes Processed
// records older than RetentionPeriod so stores don't grow forever. Claimed records are never swept
// here — a live claim either finishes (Processed, eligible for this worker later) or gets stolen
// once stale (InboxPipelineBehavior/IInboxStore's own expiry handles that, not retention).
//
// No IPartitionLeaseStore-style coordination needed — DeleteProcessedBeforeAsync only ever touches
// terminal rows, so multiple instances running their own cleanup tick concurrently is naturally safe
// (deleting an already-deleted row is a no-op).
//
// Each tick opens its own scope and re-resolves IInboxStore from it — same reasoning as
// OutboxCleanupWorker: a scope-aware backend can't be held across ticks by this Singleton service.
internal sealed class InboxCleanupWorker(
    IInboxRegistry registry,
    IServiceProvider serviceProvider,
    ILogger<InboxCleanupWorker> logger,
    IInboxOptions options) : BackgroundService
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
                    await scope.ServiceProvider.GetRequiredService<IInboxStore>()
                        .DeleteProcessedBeforeAsync(cutoff, stoppingToken);

                foreach (var messageType in keyedMessageTypes)
                    await scope.ServiceProvider.GetRequiredKeyedService<IInboxStore>(messageType)
                        .DeleteProcessedBeforeAsync(cutoff, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Inbox cleanup tick failed");
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

using FxLink.Abstractions;
using FxLink.Entities;
using FxLink.Implementations;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FxLink.BackgroundServices;

// Dispatch-path half of the Outbox pattern. Runs one independent polling loop per registered
// outbox (the default one, plus one per TMessage that opted into its own via MessageOutbox) — see
// IOutboxRegistry for why it has to be discovered this way instead of via IEnumerable<T>.
//
// Each tick opens its own scope and re-resolves IOutboxStore/IPartitionLeaseStore from it, rather
// than resolving once for the worker's whole lifetime — a scope-aware backend (e.g. EF Core, backed
// by a Scoped DbContext) can't safely be held by this Singleton hosted service across ticks the way
// InMemory's Singleton store can.
internal sealed class OutboxDispatcherWorker(
    IOutboxRegistry registry,
    IServiceProvider serviceProvider,
    OutboxRowSender rowSender,
    ILogger<OutboxDispatcherWorker> logger,
    IOutboxDispatcherOptions outboxDispatcherOptions) : BackgroundService
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var loops = new List<Task>();
        if (registry.HasDefault) loops.Add(RunStoreLoopAsync(null, stoppingToken));
        loops.AddRange(registry.KeyedMessageTypes.Select(messageType => RunStoreLoopAsync(messageType, stoppingToken)));

        return loops.Count == 0 ? Task.CompletedTask : Task.WhenAll(loops);
    }

    // messageType null = the default/unkeyed outbox.
    private static (IOutboxStore Outbox, IPartitionLeaseStore Leases) ResolveStore(IServiceProvider provider,
        Type messageType) => messageType is null
        ? (provider.GetRequiredService<IOutboxStore>(), provider.GetRequiredService<IPartitionLeaseStore>())
        : (provider.GetRequiredKeyedService<IOutboxStore>(messageType),
            provider.GetRequiredKeyedService<IPartitionLeaseStore>(messageType));

    private async Task RunStoreLoopAsync(Type messageType, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var (store, leases) = ResolveStore(scope.ServiceProvider, messageType);

                var partitionKeys = await store
                    .GetPendingPartitionKeysAsync(outboxDispatcherOptions.MaxPartitionsPerTick, stoppingToken);
                var tasks = partitionKeys.Select(key => DispatchPartitionAsync(store, leases, key, stoppingToken));
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch tick failed");
            }

            try
            {
                await Task.Delay(outboxDispatcherOptions.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchPartitionAsync(IOutboxStore store, IPartitionLeaseStore leases, Guid partitionKey,
        CancellationToken stoppingToken)
    {
        var version = await leases.TryAcquireAsync(partitionKey, _instanceId, outboxDispatcherOptions.LeaseDuration,
            stoppingToken);
        if (version is not { } leasesVersion) return; // another instance currently owns this partition

        try
        {
            var lastRenewal = DateTime.UtcNow;
            var messages = await store.GetPendingByPartitionAsync(partitionKey,
                outboxDispatcherOptions.MaxMessagesPerPartitionPerTick, stoppingToken);

            foreach (var message in messages)
            {
                if (stoppingToken.IsCancellationRequested) return;

                if (DateTime.UtcNow - lastRenewal > outboxDispatcherOptions.LeaseRenewInterval)
                {
                    var renewed = await leases.RenewAsync(partitionKey, _instanceId, leasesVersion,
                        outboxDispatcherOptions.LeaseDuration, stoppingToken);
                    if (renewed is not { } renewedVersion)
                        return; // lease was reclaimed mid-batch — stop touching this partition
                    leasesVersion = renewedVersion;
                    lastRenewal = DateTime.UtcNow;
                }

                if (!await TryDispatchOneAsync(store, message, leasesVersion, stoppingToken))
                    return; // either fencing rejected the write, or ordering requires stopping here
            }
        }
        finally
        {
            await leases.ReleaseAsync(partitionKey, _instanceId, leasesVersion, CancellationToken.None);
        }
    }

    // Returns false when the caller must stop processing this partition for the rest of this tick —
    // either the lease is gone (fencing rejected a Mark*Async call) or a message failed and hasn't
    // hit the poison threshold yet, so it must be retried in place next tick to preserve ordering.
    private async Task<bool> TryDispatchOneAsync(IOutboxStore store, OutboxMessage message, long leaseVersion,
        CancellationToken stoppingToken)
    {
        try
        {
            await rowSender.SendAsync(message, stoppingToken);
            return await store.MarkDispatchedAsync(message.Id, leaseVersion, stoppingToken);
        }
        catch (Exception ex)
        {
            var attempts = message.AttemptCount + 1;
            logger.LogError(ex, "Failed to dispatch outbox message {MessageId} (attempt {Attempt})", message.Id,
                attempts);

            if (attempts < outboxDispatcherOptions.MaxAttemptsBeforeDeadLetter)
            {
                await store.MarkFailedAsync(message.Id, leaseVersion, ex.Message, stoppingToken);
                // Stop this partition for the rest of this tick either way: if the mark succeeded,
                // AttemptCount is now recorded and this message must be retried in place (same
                // Sequence position) before anything behind it, to preserve ordering. If it failed
                // (fencing rejected it), the lease is already gone and touching this partition
                // further isn't safe regardless.
                return false;
            }

            // Poison: dead-letter it so it stops blocking everything behind it in this partition,
            // then let the caller continue on to the next message.
            return await store.MarkDeadLetteredAsync(message.Id, leaseVersion, ex.Message, stoppingToken);
        }
    }
}

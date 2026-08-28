using FxLink.Abstractions;
using FxLink.BackgroundServices;
using FxLink.Entities;
using FxLink.InMemory;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FxLink.Tests.BackgroundServices;

public class OutboxCleanupWorkerTests
{
    private sealed record Payload(string Value);

    private static OutboxMessage RowFor(Guid partitionKey) => new()
    {
        PartitionKey = partitionKey,
        MessageType = typeof(Payload).AssemblyQualifiedName,
        Payload = "{}"
    };

    private static OutboxDispatcherOptions FastOptions => new()
    {
        RetentionPeriod = TimeSpan.FromMilliseconds(50),
        CleanupInterval = TimeSpan.FromMilliseconds(30)
    };

    private static IServiceProvider BuildProvider(IOutboxStore outboxStore)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(outboxStore);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Deletes_dispatched_and_dead_lettered_rows_once_they_exceed_the_retention_period()
    {
        var leaseStore = new InMemoryPartitionLeaseStore();
        var outboxStore = new InMemoryOutboxStore(leaseStore);
        var registry = new OutboxRegistry();
        registry.RegisterDefault();

        var dispatched = RowFor(Guid.NewGuid());
        var deadLettered = RowFor(Guid.NewGuid());
        var stillPending = RowFor(Guid.NewGuid());
        await outboxStore.EnqueueAsync(dispatched);
        await outboxStore.EnqueueAsync(deadLettered);
        await outboxStore.EnqueueAsync(stillPending);

        var version1 = await leaseStore.TryAcquireAsync(dispatched.PartitionKey, "owner", TimeSpan.FromSeconds(30));
        await outboxStore.MarkDispatchedAsync(dispatched.Id, version1!.Value);
        var version2 = await leaseStore.TryAcquireAsync(deadLettered.PartitionKey, "owner", TimeSpan.FromSeconds(30));
        await outboxStore.MarkDeadLetteredAsync(deadLettered.Id, version2!.Value, "boom");

        var provider = BuildProvider(outboxStore);
        var worker = new OutboxCleanupWorker(registry, provider,
            provider.GetRequiredService<ILogger<OutboxCleanupWorker>>(), FastOptions);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(300);
        await worker.StopAsync(CancellationToken.None);

        outboxStore.Contains(dispatched.Id).ShouldBeFalse();
        outboxStore.Contains(deadLettered.Id).ShouldBeFalse();
        outboxStore.Contains(stillPending.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Leaves_recently_dispatched_rows_alone_until_the_retention_period_elapses()
    {
        var leaseStore = new InMemoryPartitionLeaseStore();
        var outboxStore = new InMemoryOutboxStore(leaseStore);
        var registry = new OutboxRegistry();
        registry.RegisterDefault();

        var dispatched = RowFor(Guid.NewGuid());
        await outboxStore.EnqueueAsync(dispatched);
        var version = await leaseStore.TryAcquireAsync(dispatched.PartitionKey, "owner", TimeSpan.FromSeconds(30));
        await outboxStore.MarkDispatchedAsync(dispatched.Id, version!.Value);

        var provider = BuildProvider(outboxStore);
        var longRetention = new OutboxDispatcherOptions
        {
            RetentionPeriod = TimeSpan.FromHours(1),
            CleanupInterval = TimeSpan.FromMilliseconds(30)
        };
        var worker = new OutboxCleanupWorker(registry, provider,
            provider.GetRequiredService<ILogger<OutboxCleanupWorker>>(), longRetention);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await worker.StopAsync(CancellationToken.None);

        outboxStore.Contains(dispatched.Id).ShouldBeTrue();
    }
}

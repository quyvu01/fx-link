using System.Text.Json;
using FxLink.Abstractions;
using FxLink.BackgroundServices;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Implementations;
using FxLink.InMemory;
using FxLink.PipelineBehaviors;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FxLink.Tests.BackgroundServices;

public class OutboxDispatcherWorkerTests
{
    private sealed record Payload(string Value);

    private sealed class RecordingClientConnector<TMessage>(int expectedCount = 1) : IClientConnector<TMessage>
        where TMessage : class
    {
        private readonly TaskCompletionSource _completion = new();
        public List<TMessage> Received { get; } = [];

        public Task WhenReceived => _completion.Task;

        public Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
        {
            lock (Received)
            {
                Received.Add(message);
                if (Received.Count >= expectedCount) _completion.TrySetResult();
            }

            return Task.CompletedTask;
        }
    }

    private static OutboxDispatcherOptions FastOptions => new()
    {
        PollInterval = TimeSpan.FromMilliseconds(30),
        LeaseDuration = TimeSpan.FromSeconds(5),
        LeaseRenewInterval = TimeSpan.FromSeconds(2)
    };

    private static OutboxMessage RowFor(Payload payload, Guid partitionKey)
    {
        IHeaders headers = new HeaderBag();
        return new OutboxMessage
        {
            PartitionKey = partitionKey,
            MessageType = typeof(Payload).AssemblyQualifiedName,
            Payload = JsonSerializer.Serialize(payload, DistributedConfigurators.JsonSerializerOptions),
            SerializedHeaders = JsonSerializer.Serialize(headers, DistributedConfigurators.JsonSerializerOptions)
        };
    }

    private static (IServiceProvider Provider, InMemoryOutboxStore Outbox, RecordingClientConnector<Payload> Connector)
        BuildSystem(int expectedSendCount = 1)
    {
        var leaseStore = new InMemoryPartitionLeaseStore();
        var outboxStore = new InMemoryOutboxStore(leaseStore);
        var connector = new RecordingClientConnector<Payload>(expectedSendCount);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOutboxStore>(outboxStore);
        services.AddSingleton<IPartitionLeaseStore>(leaseStore);
        services.AddSingleton<IClientConnector<Payload>>(connector);
        services.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        services.AddSingleton(typeof(OutboxTypedRowSender<>));
        services.AddSingleton<OutboxRowSender>();
        var provider = services.BuildServiceProvider();

        return (provider, outboxStore, connector);
    }

    [Fact]
    public async Task Dispatches_an_enqueued_message_and_marks_it_dispatched()
    {
        var (provider, outboxStore, connector) = BuildSystem();
        var registry = new OutboxRegistry();
        registry.RegisterDefault();

        var partitionKey = Guid.NewGuid();
        await outboxStore.EnqueueAsync(RowFor(new Payload("hello"), partitionKey));

        var worker = new OutboxDispatcherWorker(registry, provider, provider.GetRequiredService<OutboxRowSender>(),
            provider.GetRequiredService<ILogger<OutboxDispatcherWorker>>(), FastOptions);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await connector.WhenReceived.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        connector.Received.ShouldBe([new Payload("hello")]);
        (await outboxStore.GetPendingByPartitionAsync(partitionKey, 10)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Preserves_order_within_a_single_partition()
    {
        var (provider, outboxStore, connector) = BuildSystem(expectedSendCount: 2);
        var registry = new OutboxRegistry();
        registry.RegisterDefault();

        var partitionKey = Guid.NewGuid();
        await outboxStore.EnqueueAsync(RowFor(new Payload("first"), partitionKey));
        await outboxStore.EnqueueAsync(RowFor(new Payload("second"), partitionKey));

        var worker = new OutboxDispatcherWorker(registry, provider, provider.GetRequiredService<OutboxRowSender>(),
            provider.GetRequiredService<ILogger<OutboxDispatcherWorker>>(), FastOptions);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await connector.WhenReceived.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        connector.Received.ShouldBe([new Payload("first"), new Payload("second")]);
    }

    [Fact]
    public async Task Dispatches_independent_partitions_without_blocking_each_other()
    {
        var (provider, outboxStore, connector) = BuildSystem(expectedSendCount: 2);
        var registry = new OutboxRegistry();
        registry.RegisterDefault();

        await outboxStore.EnqueueAsync(RowFor(new Payload("partition-a"), Guid.NewGuid()));
        await outboxStore.EnqueueAsync(RowFor(new Payload("partition-b"), Guid.NewGuid()));

        var worker = new OutboxDispatcherWorker(registry, provider, provider.GetRequiredService<OutboxRowSender>(),
            provider.GetRequiredService<ILogger<OutboxDispatcherWorker>>(), FastOptions);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await connector.WhenReceived.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        connector.Received.Count.ShouldBe(2);
        connector.Received.ShouldContain(new Payload("partition-a"));
        connector.Received.ShouldContain(new Payload("partition-b"));
    }
}

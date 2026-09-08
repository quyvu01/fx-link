using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Entities;
using FxLink.Implementations;
using FxLink.InternalPipelineBehaviors;
using FxLink.PipelineBehaviors;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Implementations;

public class OutboxRowSenderTests
{
    private sealed record Payload(string Value);

    public interface IStockCreatedContract
    {
        string Name { get; set; }
        string Code { get; set; }
    }

    private sealed class StockCreated : IStockCreatedContract
    {
        public string Name { get; set; }
        public string Code { get; set; }
    }

    private sealed class FakeClientConnector<TMessage> : IClientConnector<TMessage> where TMessage : class
    {
        public TMessage ReceivedMessage { get; private set; }
        public IContext ReceivedContext { get; private set; }

        public Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
        {
            ReceivedMessage = message;
            ReceivedContext = context;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPipelineBehavior<TMessage> : IPublisherPipelineBehavior<TMessage>
        where TMessage : class
    {
        public bool WasInvoked { get; private set; }

        public Task PublishAsync(TMessage message, IPublishContext context, PublisherHandlerDelegate next,
            CancellationToken token = default)
        {
            WasInvoked = true;
            return next.Invoke(token);
        }
    }

    private sealed class FakeOutboxStore : IOutboxStore
    {
        public bool WasEnqueued { get; private set; }
        public Task EnqueueAsync(OutboxMessage message, CancellationToken token = default)
        {
            WasEnqueued = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Guid>> GetPendingPartitionKeysAsync(int max, CancellationToken token = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<OutboxMessage>> GetPendingByPartitionAsync(Guid partitionKey, int max,
            CancellationToken token = default) => throw new NotSupportedException();

        public Task<bool> MarkDispatchedAsync(Guid outboxMessageId, long leaseVersion,
            CancellationToken token = default) => throw new NotSupportedException();

        public Task<bool> MarkFailedAsync(Guid outboxMessageId, long leaseVersion, string error,
            CancellationToken token = default) => throw new NotSupportedException();

        public Task<bool> MarkDeadLetteredAsync(Guid outboxMessageId, long leaseVersion, string reason,
            CancellationToken token = default) => throw new NotSupportedException();

        public Task DeleteDispatchedBeforeAsync(DateTime cutoff, CancellationToken token = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeOutboxStoreResolver<TMessage>(IOutboxStore store) : IOutboxStoreResolver<TMessage>
        where TMessage : class
    {
        public IOutboxStore GetOutboxStore() => store;
    }

    private static ServiceProvider BuildProvider<TMessage>(IClientConnector<TMessage> connector)
        where TMessage : class
    {
        var services = new ServiceCollection();
        services.AddSingleton(connector);
        services.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        services.AddSingleton(typeof(OutboxTypedRowSender<>));
        return services.BuildServiceProvider();
    }

    private static OutboxMessage RowFor<TMessage>(TMessage message, Guid partitionKey, IHeaders headers = null)
        where TMessage : class => new()
    {
        PartitionKey = partitionKey,
        MessageType = typeof(TMessage).AssemblyQualifiedName,
        Payload = JsonSerializer.Serialize(message, DistributedConfigurators.JsonSerializerOptions),
        SerializedHeaders = JsonSerializer.Serialize(headers ?? new HeaderBag(),
            DistributedConfigurators.JsonSerializerOptions),
        DelayTime = TimeSpan.FromSeconds(5),
        TimeToLive = TimeSpan.FromMinutes(1),
        ScheduleToken = Guid.NewGuid(),
        RequesterId = Guid.NewGuid()
    };

    [Fact]
    public async Task SendAsync_deserializes_the_payload_and_forwards_to_the_resolved_connector()
    {
        var connector = new FakeClientConnector<Payload>();
        await using var provider = BuildProvider<Payload>(connector);

        var partitionKey = Guid.NewGuid();
        var row = RowFor(new Payload("hello"), partitionKey);

        await new OutboxRowSender(provider).SendAsync(row);

        connector.ReceivedMessage.ShouldBe(new Payload("hello"));
        connector.ReceivedContext.CorrelationId.ShouldBe(partitionKey);
    }

    [Fact]
    public async Task SendAsync_reconstructs_the_scheduling_fields_onto_the_context()
    {
        var connector = new FakeClientConnector<Payload>();
        await using var provider = BuildProvider<Payload>(connector);

        var row = RowFor(new Payload("a"), Guid.NewGuid());

        await new OutboxRowSender(provider).SendAsync(row);

        var context = connector.ReceivedContext.ShouldBeOfType<PublishContext>();
        context.DelayTime.ShouldBe(row.DelayTime);
        context.TimeToLive.ShouldBe(row.TimeToLive);
        context.ScheduleToken.ShouldBe(row.ScheduleToken);
        context.RequesterId.ShouldBe(row.RequesterId);
    }

    [Fact]
    public async Task SendAsync_reconstructs_headers_from_SerializedHeaders()
    {
        var connector = new FakeClientConnector<Payload>();
        await using var provider = BuildProvider<Payload>(connector);

        var headers = new HeaderBag();
        headers.Set("token", "abc-123");
        var row = RowFor(new Payload("a"), Guid.NewGuid(), headers);

        await new OutboxRowSender(provider).SendAsync(row);

        connector.ReceivedContext.Headers.Get<string>("token").ShouldBe("abc-123");
    }

    [Fact]
    public async Task SendAsync_resolves_the_connector_keyed_by_an_interface_typed_message()
    {
        var connector = new FakeClientConnector<IStockCreatedContract>();
        await using var provider = BuildProvider<IStockCreatedContract>(connector);

        IStockCreatedContract message = new StockCreated { Name = "Widget", Code = "W-1" };
        var row = RowFor(message, Guid.NewGuid());

        await new OutboxRowSender(provider).SendAsync(row);

        connector.ReceivedMessage.ShouldNotBeNull();
        connector.ReceivedMessage.Name.ShouldBe("Widget");
        connector.ReceivedMessage.Code.ShouldBe("W-1");
    }

    [Fact]
    public async Task SendAsync_throws_when_the_message_type_cannot_be_resolved()
    {
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();

        var row = new OutboxMessage
        {
            PartitionKey = Guid.NewGuid(),
            MessageType = "Not.A.Real.Type, NotAnAssembly",
            Payload = JsonSerializer.Serialize(new Payload("a"), DistributedConfigurators.JsonSerializerOptions)
        };

        await Should.ThrowAsync<InvalidOperationException>(() => new OutboxRowSender(provider).SendAsync(row));
    }

    [Fact]
    public async Task SendAsync_still_runs_other_registered_publisher_pipeline_behaviors()
    {
        var connector = new FakeClientConnector<Payload>();
        var recordingBehavior = new RecordingPipelineBehavior<Payload>();
        var services = new ServiceCollection();
        services.AddSingleton<IClientConnector<Payload>>(connector);
        services.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        services.AddSingleton(typeof(OutboxTypedRowSender<>));
        services.AddSingleton<IPublisherPipelineBehavior<Payload>>(recordingBehavior);
        await using var provider = services.BuildServiceProvider();

        var row = RowFor(new Payload("a"), Guid.NewGuid());

        await new OutboxRowSender(provider).SendAsync(row);

        recordingBehavior.WasInvoked.ShouldBeTrue();
        connector.ReceivedMessage.ShouldBe(new Payload("a"));
    }

    [Fact]
    public async Task SendAsync_excludes_OutboxPublisherPipelineBehavior_so_it_does_not_re_enqueue()
    {
        var connector = new FakeClientConnector<Payload>();
        var outboxStore = new FakeOutboxStore();
        var services = new ServiceCollection();
        services.AddSingleton<IClientConnector<Payload>>(connector);
        services.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        services.AddSingleton(typeof(OutboxTypedRowSender<>));
        services.AddSingleton<IOutboxStoreResolver<Payload>>(new FakeOutboxStoreResolver<Payload>(outboxStore));
        services.AddSingleton<IPublisherPipelineBehavior<Payload>, OutboxPublisherPipelineBehavior<Payload>>();
        await using var provider = services.BuildServiceProvider();

        var row = RowFor(new Payload("a"), Guid.NewGuid());

        await new OutboxRowSender(provider).SendAsync(row);

        // If OutboxPublisherPipelineBehavior weren't excluded, it would enqueue this row again
        // instead of ever reaching the real connector.
        outboxStore.WasEnqueued.ShouldBeFalse();
        connector.ReceivedMessage.ShouldBe(new Payload("a"));
    }
}

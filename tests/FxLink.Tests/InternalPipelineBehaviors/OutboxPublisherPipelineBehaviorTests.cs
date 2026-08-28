using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.InternalPipelineBehaviors;
using Shouldly;
using Xunit;

namespace FxLink.Tests.InternalPipelineBehaviors;

public class OutboxPublisherPipelineBehaviorTests
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

    private sealed class FakeOutboxStore : IOutboxStore
    {
        public readonly List<OutboxMessage> Enqueued = [];
        public CancellationToken? LastToken { get; private set; }

        public Task EnqueueAsync(OutboxMessage message, CancellationToken token = default)
        {
            Enqueued.Add(message);
            LastToken = token;
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
        public IPartitionLeaseStore GetPartitionLeaseStore() => throw new NotSupportedException();
    }

    private static PublishContext ContextFor(Guid correlationId, IHeaders headers = null) =>
        new(headers ?? new HeaderBag(), correlationId)
        {
            DelayTime = TimeSpan.FromSeconds(5),
            TimeToLive = TimeSpan.FromMinutes(1),
            ScheduleToken = Guid.NewGuid(),
            RequesterId = Guid.NewGuid()
        };

    [Fact]
    public async Task Passes_through_to_next_when_no_outbox_store_resolves()
    {
        var resolver = new FakeOutboxStoreResolver<Payload>(null);
        var behavior = new OutboxPublisherPipelineBehavior<Payload>(resolver);
        var nextCalled = false;

        await behavior.PublishAsync(new Payload("a"), ContextFor(Guid.NewGuid()), _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Enqueues_to_the_outbox_and_does_not_call_next_when_a_store_resolves()
    {
        var store = new FakeOutboxStore();
        var resolver = new FakeOutboxStoreResolver<Payload>(store);
        var behavior = new OutboxPublisherPipelineBehavior<Payload>(resolver);
        var nextCalled = false;

        await behavior.PublishAsync(new Payload("a"), ContextFor(Guid.NewGuid()), _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.ShouldBeFalse();
        store.Enqueued.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_enqueued_message_captures_the_contexts_partition_key_and_scheduling_fields()
    {
        var store = new FakeOutboxStore();
        var resolver = new FakeOutboxStoreResolver<Payload>(store);
        var behavior = new OutboxPublisherPipelineBehavior<Payload>(resolver);
        var correlationId = Guid.NewGuid();
        var context = ContextFor(correlationId);

        await behavior.PublishAsync(new Payload("a"), context, _ => Task.CompletedTask);

        var enqueued = store.Enqueued.Single();
        enqueued.PartitionKey.ShouldBe(correlationId);
        enqueued.MessageType.ShouldBe(typeof(Payload).AssemblyQualifiedName);
        enqueued.DelayTime.ShouldBe(context.DelayTime);
        enqueued.TimeToLive.ShouldBe(context.TimeToLive);
        enqueued.ScheduleToken.ShouldBe(context.ScheduleToken);
        enqueued.RequesterId.ShouldBe(context.RequesterId);
    }

    [Fact]
    public async Task The_enqueued_payload_round_trips_back_to_an_equivalent_message()
    {
        var store = new FakeOutboxStore();
        var resolver = new FakeOutboxStoreResolver<Payload>(store);
        var behavior = new OutboxPublisherPipelineBehavior<Payload>(resolver);

        await behavior.PublishAsync(new Payload("hello"), ContextFor(Guid.NewGuid()), _ => Task.CompletedTask);

        var roundTripped = JsonSerializer.Deserialize<Payload>(store.Enqueued.Single().Payload,
            DistributedConfigurators.JsonSerializerOptions);

        roundTripped.ShouldBe(new Payload("hello"));
    }

    [Fact]
    public async Task An_interface_typed_messages_payload_round_trips_through_the_message_contract_converter()
    {
        var store = new FakeOutboxStore();
        var resolver = new FakeOutboxStoreResolver<IStockCreatedContract>(store);
        var behavior = new OutboxPublisherPipelineBehavior<IStockCreatedContract>(resolver);
        IStockCreatedContract message = new StockCreated { Name = "Widget", Code = "W-1" };

        await behavior.PublishAsync(message, ContextFor(Guid.NewGuid()), _ => Task.CompletedTask);

        var enqueued = store.Enqueued.Single();
        enqueued.MessageType.ShouldBe(typeof(IStockCreatedContract).AssemblyQualifiedName);

        var roundTripped = JsonSerializer.Deserialize<IStockCreatedContract>(enqueued.Payload,
            DistributedConfigurators.JsonSerializerOptions);
        roundTripped.Name.ShouldBe("Widget");
        roundTripped.Code.ShouldBe("W-1");
    }

    [Fact]
    public async Task The_enqueued_headers_round_trip_through_HeadersJsonConverter()
    {
        var store = new FakeOutboxStore();
        var resolver = new FakeOutboxStoreResolver<Payload>(store);
        var behavior = new OutboxPublisherPipelineBehavior<Payload>(resolver);
        var headers = new HeaderBag();
        headers.Set("token", "abc-123");
        var context = ContextFor(Guid.NewGuid(), headers);

        await behavior.PublishAsync(new Payload("a"), context, _ => Task.CompletedTask);

        var roundTripped = JsonSerializer.Deserialize<IHeaders>(store.Enqueued.Single().SerializedHeaders,
            DistributedConfigurators.JsonSerializerOptions);
        roundTripped.Get<string>("token").ShouldBe("abc-123");
    }

    [Fact]
    public async Task The_cancellation_token_propagates_to_EnqueueAsync()
    {
        var store = new FakeOutboxStore();
        var resolver = new FakeOutboxStoreResolver<Payload>(store);
        var behavior = new OutboxPublisherPipelineBehavior<Payload>(resolver);
        using var cts = new CancellationTokenSource();

        await behavior.PublishAsync(new Payload("a"), ContextFor(Guid.NewGuid()), _ => Task.CompletedTask, cts.Token);

        store.LastToken.ShouldBe(cts.Token);
    }
}

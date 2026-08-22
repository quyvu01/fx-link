using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Implementations;

public class BatchDispatcherTests
{
    private sealed record Payload(string Value);

    private sealed class MarkerConsumer : IConsumer<IBatch<Payload>>
    {
        public Task ConsumeAsync(IConsumeContext<IBatch<Payload>> context, CancellationToken token = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingConnector : IConsumerConnector<IBatch<Payload>>
    {
        public IConsumeContext<IBatch<Payload>> LastContext { get; private set; }
        public Type LastConsumerType { get; private set; }

        public Task ConsumeAsync(IConsumeContext<IBatch<Payload>> context, Type consumerType,
            CancellationToken token = default)
        {
            LastContext = context;
            LastConsumerType = consumerType;
            return Task.CompletedTask;
        }
    }

    private static IConsumeContext<Payload> ContextFor(string value) =>
        new ConsumeContext<Payload>(new Payload(value), PublishContext.New(), requesterId: null);

    [Fact]
    public async Task DispatchAsync_wraps_the_messages_into_a_fresh_batch_context_and_invokes_the_connector()
    {
        var connector = new RecordingConnector();
        var services = new ServiceCollection();
        services.AddSingleton<IConsumerConnector<IBatch<Payload>>>(connector);
        await using var provider = services.BuildServiceProvider();

        var dispatcher = new BatchDispatcher<Payload>(provider);
        var messages = new List<IConsumeContext<Payload>> { ContextFor("a"), ContextFor("b") };

        await dispatcher.DispatchAsync(messages, typeof(MarkerConsumer));

        connector.LastConsumerType.ShouldBe(typeof(MarkerConsumer));
        connector.LastContext.ShouldNotBeNull();
        connector.LastContext.Message.Length.ShouldBe(2);
        connector.LastContext.Message.Select(x => x.Message.Value).ShouldBe(["a", "b"]);
        connector.LastContext.RequesterId.ShouldBeNull();
        connector.LastContext.CorrelationId.ShouldNotBe(Guid.Empty);
        connector.LastContext.MessageId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Each_dispatch_mints_its_own_CorrelationId_and_MessageId()
    {
        var connector = new RecordingConnector();
        var services = new ServiceCollection();
        services.AddSingleton<IConsumerConnector<IBatch<Payload>>>(connector);
        await using var provider = services.BuildServiceProvider();
        var dispatcher = new BatchDispatcher<Payload>(provider);
        var messages = new List<IConsumeContext<Payload>> { ContextFor("a") };

        await dispatcher.DispatchAsync(messages, typeof(MarkerConsumer));
        var first = connector.LastContext;
        await dispatcher.DispatchAsync(messages, typeof(MarkerConsumer));
        var second = connector.LastContext;

        first.CorrelationId.ShouldNotBe(second.CorrelationId);
        first.MessageId.ShouldNotBe(second.MessageId);
    }

    private sealed class ObservedInstances
    {
        public List<Guid> Ids { get; } = [];
    }

    private sealed class ScopedRecordingConnector(ObservedInstances observed) : IConsumerConnector<IBatch<Payload>>
    {
        private readonly Guid _instanceId = Guid.NewGuid();

        public Task ConsumeAsync(IConsumeContext<IBatch<Payload>> context, Type consumerType,
            CancellationToken token = default)
        {
            observed.Ids.Add(_instanceId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchAsync_uses_a_fresh_scope_per_call_not_the_root_provider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObservedInstances>();
        services.AddScoped<IConsumerConnector<IBatch<Payload>>, ScopedRecordingConnector>();
        await using var provider = services.BuildServiceProvider();

        var dispatcher = new BatchDispatcher<Payload>(provider);
        var messages = new List<IConsumeContext<Payload>> { ContextFor("a") };

        await dispatcher.DispatchAsync(messages, typeof(MarkerConsumer));
        await dispatcher.DispatchAsync(messages, typeof(MarkerConsumer));

        var observed = provider.GetRequiredService<ObservedInstances>();
        observed.Ids.Count.ShouldBe(2);
        observed.Ids[0].ShouldNotBe(observed.Ids[1]);
    }
}

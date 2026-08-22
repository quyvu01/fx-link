using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Extensions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Registries;

// Proves the wiring end to end: a throwing batch consumer, reached through the real
// AddConsumer/orchestrator/accumulator/dispatcher chain, gets its exception handled by
// BatchRetryPipelineBehavior<TMessage> (registered as a closed generic in Configurator.AddConsumer)
// — and NOT by RetryPipelineBehavior<IBatch<TMessage>>, which the open-generic registration would
// otherwise also match (see the guard added to RetryPipelineBehavior).
public class BatchConsumerFailureEndToEndTests
{
    private interface IE2EBatchMessage
    {
        string Value { get; }
    }

    private sealed record E2EBatchMessage(string Value) : IE2EBatchMessage;

    private sealed class ThrowingBatchConsumer : IConsumer<IBatch<IE2EBatchMessage>>
    {
        public Task ConsumeAsync(IConsumeContext<IBatch<IE2EBatchMessage>> context, CancellationToken token = default)
            => throw new InvalidOperationException("batch consumer boom");
    }

    private sealed class ThrowingBatchConsumerDefinition : ConsumerDefinition<ThrowingBatchConsumer>
    {
        public override void Configure(IConsumerConfigurator<ThrowingBatchConsumer> options)
        {
            options.UseBatching<IE2EBatchMessage>(c => c
                .SetMessageLimit(2)
                .SetTimeLimit(TimeSpan.FromMinutes(5))
                .SetConcurrencyLimit(1));
            options.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1)));
        }
    }

    private sealed class SpyPublisher : IPublisher
    {
        private readonly TaskCompletionSource _allReceived = new();
        private readonly int _expectedCount;
        public List<object> Published { get; } = [];

        public SpyPublisher(int expectedCount) => _expectedCount = expectedCount;

        public Task WaitForAllAsync(TimeSpan timeout) => _allReceived.Task.WaitAsync(timeout);

        public Task PublishAsync<TMessage>(TMessage message, Action<IPublishContext> contextOptions,
            CancellationToken token = default) where TMessage : class
        {
            lock (Published)
            {
                Published.Add(message);
                if (Published.Count >= _expectedCount) _allReceived.TrySetResult();
            }

            return Task.CompletedTask;
        }

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken token = default)
            where TMessage : class => PublishAsync(message, null, token);
    }

    private static IConsumeContext<IE2EBatchMessage> ContextFor(string value) =>
        new ConsumeContext<IE2EBatchMessage>(new E2EBatchMessage(value), PublishContext.New(), requesterId: null);

    [Fact]
    public async Task A_throwing_batch_consumer_republishes_every_message_in_the_batch_individually()
    {
        var spyPublisher = new SpyPublisher(expectedCount: 2);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(cfg =>
        {
            cfg.UseInMemory();
            cfg.AddConsumer<ThrowingBatchConsumer>();
            cfg.AddConsumerDefinition<ThrowingBatchConsumerDefinition>();
        });
        // Overrides AddFxLink's own IPublisher registration — last registration wins in
        // Microsoft.Extensions.DependencyInjection, regardless of the differing lifetimes.
        services.AddSingleton<IPublisher>(spyPublisher);

        await using var provider = services.BuildServiceProvider();
        var connector = provider.GetRequiredService<IConsumerConnector<IE2EBatchMessage>>();

        await connector.ConsumeAsync(ContextFor("a"), typeof(ThrowingBatchConsumer));
        await connector.ConsumeAsync(ContextFor("b"), typeof(ThrowingBatchConsumer)); // MessageLimit(2) -> flush -> throws

        await spyPublisher.WaitForAllAsync(TimeSpan.FromSeconds(2));

        spyPublisher.Published.Count.ShouldBe(2);
        spyPublisher.Published.OfType<IE2EBatchMessage>().Select(x => x.Value).ShouldBe(["a", "b"], ignoreOrder: true);
    }
}

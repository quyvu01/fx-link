using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Extensions;
using FxLink.Implementations;
using FxLink.InternalPipelineBehaviors;
using FxLink.Registries;
using FxLink.Statics;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace FxLink.Tests.InternalPipelineBehaviors;

public class BatchRetryPipelineBehaviorTests
{
    private sealed record Payload(string Value);

    private sealed class TestConsumer : IConsumer<IBatch<Payload>>
    {
        public Task ConsumeAsync(IConsumeContext<IBatch<Payload>> context, CancellationToken token = default) =>
            Task.CompletedTask;
    }

    private sealed class TestConsumerDefinition : ConsumerDefinition<TestConsumer>
    {
        // A single retry interval: retryCount 0 gets retried once, retryCount 1+ is exhausted.
        public override void Configure(IConsumerConfigurator<TestConsumer> options) =>
            options.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1)));
    }

    private sealed class SpyPublisher : IPublisher
    {
        public List<(object Message, TimeSpan? DelayTime)> Published { get; } = [];

        public Task PublishAsync<TMessage>(TMessage message, Action<IPublishContext> contextOptions,
            CancellationToken token = default) where TMessage : class
        {
            var ctx = PublishContext.New();
            contextOptions?.Invoke(ctx);
            Published.Add((message, ctx.DelayTime));
            return Task.CompletedTask;
        }

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken token = default)
            where TMessage : class => PublishAsync(message, null, token);
    }

    private static IConsumeContext<Payload> ContextFor(string value) =>
        new ConsumeContext<Payload>(new Payload(value), PublishContext.New(), requesterId: null);

    private static (BatchRetryPipelineBehavior<Payload> Behavior, SpyPublisher Publisher, IServiceProvider Provider)
        BuildBehavior()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IConsumerConfiguratorResolver<>), typeof(ConsumerConfiguratorResolver<>));
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IConsumerDefinition<TestConsumer>),
            typeof(TestConsumerDefinition), ServiceLifetime.Singleton));
        var publisher = new SpyPublisher();
        services.AddSingleton<IPublisher>(publisher);
        var provider = services.BuildServiceProvider();
        return (new BatchRetryPipelineBehavior<Payload>(provider), publisher, provider);
    }

    private static IConsumeContext<IBatch<Payload>> BuildBatchContext(IServiceProvider provider,
        params IConsumeContext<Payload>[] messages)
    {
        var batch = new MessageBatch<Payload>(messages);
        var context = new ConsumeContext<IBatch<Payload>>(batch, new HeaderBag(), Id.New(), requesterId: null);
        context.SetPayload(provider);
        context.SetPayload(new ConsumerContextWrapped(typeof(TestConsumer)));
        return context;
    }

    private static ConsumerHandlerDelegate AlwaysThrows(Exception ex) => _ => throw ex;

    [Fact]
    public async Task A_fresh_message_gets_retried_with_the_configured_interval_and_an_incremented_RetryCountKey()
    {
        var (behavior, publisher, provider) = BuildBehavior();
        var message = ContextFor("fresh");
        var batchContext = BuildBatchContext(provider, message);

        await behavior.ConsumeAsync(batchContext, AlwaysThrows(new InvalidOperationException("boom")));

        publisher.Published.Count.ShouldBe(1);
        publisher.Published[0].Message.ShouldBe(message.Message);
        publisher.Published[0].DelayTime.ShouldBe(TimeSpan.FromSeconds(1));
        message.Headers.Get<int>(DistributedConfigurators.Headers.RetryCountKey).ShouldBe(1);
        message.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey)
            .ShouldBe(DistributedConfigurators.DeliveryKinds.Retry);
    }

    [Fact]
    public async Task A_message_that_already_exhausted_its_retries_is_dead_lettered_instead()
    {
        var (behavior, publisher, provider) = BuildBehavior();
        var message = ContextFor("exhausted");
        message.Headers.Set(DistributedConfigurators.Headers.RetryCountKey, 1); // the only interval already used
        var batchContext = BuildBatchContext(provider, message);
        var ex = new InvalidOperationException("boom");

        await behavior.ConsumeAsync(batchContext, AlwaysThrows(ex));

        publisher.Published.Count.ShouldBe(1);
        publisher.Published[0].DelayTime.ShouldBeNull();
        message.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey)
            .ShouldBe(DistributedConfigurators.DeliveryKinds.DeadLetter);
        message.Headers.Get<string>(DistributedConfigurators.Headers.ExceptionTypeKey)
            .ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task Each_message_in_a_failed_batch_is_judged_against_its_own_retry_count_independently()
    {
        var (behavior, publisher, provider) = BuildBehavior();
        var freshMessage = ContextFor("fresh");
        var exhaustedMessage = ContextFor("exhausted");
        exhaustedMessage.Headers.Set(DistributedConfigurators.Headers.RetryCountKey, 1);
        var batchContext = BuildBatchContext(provider, freshMessage, exhaustedMessage);

        await behavior.ConsumeAsync(batchContext, AlwaysThrows(new InvalidOperationException("boom")));

        publisher.Published.Count.ShouldBe(2);
        freshMessage.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey)
            .ShouldBe(DistributedConfigurators.DeliveryKinds.Retry);
        exhaustedMessage.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey)
            .ShouldBe(DistributedConfigurators.DeliveryKinds.DeadLetter);
    }

    [Fact]
    public async Task When_next_succeeds_no_message_is_republished()
    {
        var (behavior, publisher, provider) = BuildBehavior();
        var batchContext = BuildBatchContext(provider, ContextFor("a"), ContextFor("b"));

        await behavior.ConsumeAsync(batchContext, _ => Task.CompletedTask);

        publisher.Published.ShouldBeEmpty();
    }
}

using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Extensions;
using FxLink.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.InternalPipelineBehaviors;

// Runs when a batch dispatch (IConsumer<IBatch<TMessage>>) throws. Unlike RetryPipelineBehavior,
// which owns exactly one message, a batch failure has no single origin — the consumer processed
// every message together (e.g. one bulk insert), so there is no way to know which entry actually
// caused it. Every message in the batch is treated the same: republished individually, each judged
// against its OWN accumulated RetryCountKey — messages can enter a batch with different retry
// counts already, if they were re-buffered after a previous batch's failure (see
// BatchAccumulator's force-flush-on-retry).
//
// Deliberately NOT registered via the open-generic IConsumerPipelineBehavior<> mechanism
// (AddConsumerPipelineBehaviors) — verified empirically that .NET's DI container can't match an
// open-generic registration whose implementation closes over IConsumerPipelineBehavior<IBatch<T>>
// (nested generic construction), it throws ArgumentException at resolution time. Registered instead
// as a closed-generic service per batch consumer in Configurator.AddConsumer, the same way
// IBatchAccumulator<TMessage> is.
internal sealed class BatchRetryPipelineBehavior<TMessage>(IServiceProvider serviceProvider)
    : IConsumerPipelineBehavior<IBatch<TMessage>> where TMessage : class
{
    public async Task ConsumeAsync(IConsumeContext<IBatch<TMessage>> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        var services = context.GetPayload<IServiceProvider>();
        var logger = services.GetService<ILogger<BatchRetryPipelineBehavior<TMessage>>>();
        try
        {
            await next.Invoke(token);
        }
        catch (Exception ex)
        {
            var consumerType = context.GetPayload<ConsumerContextWrapped>().ConsumerType;
            var retryPolicy = (GetRetryPolicy(consumerType) as MessageRetryPolicy)!;
            var publisher = services.GetRequiredService<IPublisher>();

            foreach (var messageContext in context.Message)
                await HandleOneAsync(messageContext, ex, retryPolicy, publisher, logger, token);
        }
    }

    // Mirrors RetryPipelineBehavior<TMessage>'s single-message decision, applied to one entry of
    // the batch at a time. Kept as its own copy rather than a shared extraction — RetryPipelineBehavior
    // has no dedicated tests today, so refactoring it to share code here would add risk to an
    // already-relied-upon path for no behavioral benefit.
    private static async Task HandleOneAsync(IConsumeContext<TMessage> context, Exception ex,
        MessageRetryPolicy retryPolicy, IPublisher publisher, ILogger logger, CancellationToken token)
    {
        var intervals = retryPolicy.RetryIntervals;
        var ignoreExceptions = retryPolicy.IgnoreExceptions;

        var requestSemantics = context.Headers.Get<string>(DistributedConfigurators.Headers.RequestSemanticsKey);
        var requestAsPublisher = requestSemantics is DistributedConfigurators.RequestSemantics.RequestAsPublisher;

        if (ShouldIgnore(ex, ignoreExceptions))
        {
            // Unlike the single-message behavior, a request-as-publisher message inside a batch
            // can't "throw" to abort — that would also cancel every sibling message's retry. It's
            // simply left alone (not republished); nothing downstream observes this today.
            if (requestAsPublisher) return;
            await DeadLetterAsync(context, ex, publisher, logger, token);
            return;
        }

        var retryCount = context.Headers.Get<int>(DistributedConfigurators.Headers.RetryCountKey);
        if (retryCount < intervals.Length)
        {
            await RetryAsync(context, intervals[retryCount], retryCount, publisher, logger, token);
            return;
        }

        if (requestAsPublisher) return;
        await DeadLetterAsync(context, ex, publisher, logger, token);
    }

    private static async Task RetryAsync(IConsumeContext<TMessage> context, TimeSpan nextRetry, int retryCount,
        IPublisher publisher, ILogger logger, CancellationToken token)
    {
        context.Headers.Set(DistributedConfigurators.Headers.RetryCountKey, retryCount + 1);
        context.Headers.Set(DistributedConfigurators.Headers.DeliveryKindKey,
            DistributedConfigurators.DeliveryKinds.Retry);
        logger?.LogWarning(
            "Message: {@Message} in a failed batch will be retried after: {@TimeSpan}",
            context.Message, nextRetry);

        publisher.SetContext(context);
        await publisher.PublishAsync(context.Message, ctx =>
        {
            ctx.DelayTime = nextRetry;
            ctx.RequesterId = context.RequesterId;
        }, token);
    }

    private static async Task DeadLetterAsync(IConsumeContext<TMessage> context, Exception ex, IPublisher publisher,
        ILogger logger, CancellationToken token)
    {
        context.Headers.Set(DistributedConfigurators.Headers.DeliveryKindKey,
            DistributedConfigurators.DeliveryKinds.DeadLetter);
        SetExceptionHeaders(context, ex);
        publisher.SetContext(context);
        logger?.LogError(ex,
            "Message: {@Message} in a failed batch has been moved to the dead letter queue.",
            context.Message);
        await publisher.PublishAsync(context.Message, ctx => ctx.RequesterId = context.RequesterId, token);
    }

    private static void SetExceptionHeaders(IConsumeContext context, Exception ex)
    {
        context.Headers.Set(DistributedConfigurators.Headers.ExceptionTypeKey,
            ex.GetType().FullName ?? ex.GetType().Name);
        context.Headers.Set(DistributedConfigurators.Headers.ExceptionMessageKey, ex.Message);
        context.Headers.Set(DistributedConfigurators.Headers.ExceptionStackTraceKey, ex.StackTrace ?? string.Empty);
    }

    private static bool ShouldIgnore(Exception ex, Type[] ignoreExceptions)
    {
        if (ignoreExceptions is not { Length: > 0 }) return false;
        var exType = ex.GetType();
        return ignoreExceptions.Any(ignored => ignored.IsAssignableFrom(exType));
    }

    private IMessageRetryPolicy GetRetryPolicy(Type consumerType)
    {
        var consumerDefinitionResolver = (IConsumerConfiguratorResolver)serviceProvider
            .GetRequiredService(typeof(IConsumerConfiguratorResolver<>).MakeGenericType(consumerType));
        var messageRetryConfigurator = consumerDefinitionResolver.Resolve<IMessageRetryPolicy>(typeof(TMessage));
        if (messageRetryConfigurator is not null) return messageRetryConfigurator;
        var consumerConfigurator = consumerDefinitionResolver.Resolve<IMessageRetryPolicy>(consumerType);
        if (consumerConfigurator is not null) return consumerConfigurator;
        return MessageRetryPolicy.DefaultMessageRetryPolicy;
    }
}

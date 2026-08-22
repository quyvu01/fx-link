using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Extensions;
using FxLink.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FxLink.Configurators.DistributedConfigurators;

namespace FxLink.InternalPipelineBehaviors;

internal sealed class BatchRetryPipelineBehavior<TMessage>(IServiceProvider serviceProvider)
    : IConsumerPipelineBehavior<IBatch<TMessage>> where TMessage : class
{
    public async Task ConsumeAsync(IConsumeContext<IBatch<TMessage>> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        try
        {
            await next.Invoke(token);
        }
        catch (Exception ex)
        {
            var consumerType = context.GetPayload<ConsumerContextWrapped>().ConsumerType;
            var retryPolicy = (GetRetryPolicy(consumerType) as MessageRetryPolicy)!;
            var publisher = serviceProvider.GetRequiredService<IPublisher>();
            var logger = serviceProvider.GetService<ILogger<BatchRetryPipelineBehavior<TMessage>>>();

            foreach (var messageContext in context.Message)
                await HandleOneAsync(messageContext, ex, retryPolicy, publisher, logger, token);
        }
    }

    private static async Task HandleOneAsync(IConsumeContext<TMessage> context, Exception ex,
        MessageRetryPolicy retryPolicy, IPublisher publisher, ILogger logger, CancellationToken token)
    {
        var intervals = retryPolicy.RetryIntervals;
        var ignoreExceptions = retryPolicy.IgnoreExceptions;

        var requestSemantics = context.Headers.Get<string>(Headers.RequestSemanticsKey);
        var requestAsPublisher = requestSemantics is RequestSemantics.RequestAsPublisher;

        if (ShouldIgnore(ex, ignoreExceptions))
        {
            // Unlike the single-message behavior, a request-as-publisher message inside a batch
            // can't "throw" to abort — that would also cancel every sibling message's retry. It's
            // simply left alone (not republished); nothing downstream observes this today.
            if (requestAsPublisher) return;
            await DeadLetterAsync(context, ex, publisher, logger, token);
            return;
        }

        var retryCount = context.Headers.Get<int>(Headers.RetryCountKey);
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
        context.Headers.Set(Headers.RetryCountKey, retryCount + 1);
        context.Headers.Set(Headers.DeliveryKindKey, DeliveryKinds.Retry);
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
        context.Headers.Set(Headers.DeliveryKindKey,
            DeliveryKinds.DeadLetter);
        SetExceptionHeaders(context, ex);
        publisher.SetContext(context);
        logger?.LogError(ex,
            "Message: {@Message} in a failed batch has been moved to the dead letter queue.",
            context.Message);
        await publisher.PublishAsync(context.Message, ctx => ctx.RequesterId = context.RequesterId, token);
    }

    private static void SetExceptionHeaders(IConsumeContext context, Exception ex)
    {
        context.Headers.Set(Headers.ExceptionTypeKey,
            ex.GetType().FullName ?? ex.GetType().Name);
        context.Headers.Set(Headers.ExceptionMessageKey, ex.Message);
        context.Headers.Set(Headers.ExceptionStackTraceKey, ex.StackTrace ?? string.Empty);
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
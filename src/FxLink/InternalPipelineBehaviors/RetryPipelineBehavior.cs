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

internal sealed class RetryPipelineBehavior<TMessage>(IServiceProvider serviceProvider)
    : IConsumerPipelineBehavior<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        var services = context.GetPayload<IServiceProvider>();
        var logger = services.GetService<ILogger<RetryPipelineBehavior<TMessage>>>();
        try
        {
            await next.Invoke(token);
        }
        catch (Exception ex)
        {
            var consumerType = context.GetPayload<ConsumerContextWrapped>().ConsumerType;
            var retryPolicy = (GetRetryPolicy(consumerType) as MessageRetryPolicy)!;
            var intervals = retryPolicy.RetryIntervals;
            var ignoreExceptions = retryPolicy.IgnoreExceptions;

            var publisher = services.GetRequiredService<IPublisher>();
            var requestSemantics = context.Headers.Get<string>(DistributedConfigurators.Headers.RequestSemanticsKey);
            var requestAsPublisher = requestSemantics is DistributedConfigurators.RequestSemantics.RequestAsPublisher;

            if (ShouldIgnore(ex, ignoreExceptions))
            {
                if (requestAsPublisher) throw;
                context.Headers.Set(DistributedConfigurators.Headers.DeliveryKindKey,
                    DistributedConfigurators.DeliveryKinds.DeadLetter);
                SetExceptionHeaders(context, ex);
                publisher.SetContext(context);
                logger?.LogError(
                    "Message has been moved to dead letter queue due the exception ignore: {@Message} with exception: {@Exception}",
                    context.Message, new { ex.Message, ex.StackTrace });
                await publisher.PublishAsync(context.Message, ctx => ctx.RequesterId = context.RequesterId, token);
                return;
            }

            var retryCount = context.Headers.Get<int>(DistributedConfigurators.Headers.RetryCountKey);

            if (retryCount < intervals.Length)
            {
                // Broker-based retry: ack this delivery now and republish to the retry exchange
                // with a per-message TTL (props.Expiration). RabbitMQ dead-letters it back to the
                // main exchange once the TTL expires. This frees the consumer/prefetch slot
                // immediately instead of blocking it for the whole backoff duration — see
                // RabbitMqClient's retry exchange/queue (TTL + x-dead-letter-exchange) topology.
                var nextRetry = intervals[retryCount];
                context.Headers.Set(DistributedConfigurators.Headers.RetryCountKey, retryCount + 1);
                context.Headers.Set(DistributedConfigurators.Headers.DeliveryKindKey,
                    DistributedConfigurators.DeliveryKinds.Retry);
                logger?.LogWarning(
                    "Message: {@Message} processed failed with exception: {@Exception}. Retry will be processed after: {@TimeSpan}",
                    context.Message, new { ex.Message, ex.StackTrace }, nextRetry);

                publisher.SetContext(context);
                await publisher.PublishAsync(context.Message, ctx =>
                {
                    ctx.DelayTime = nextRetry;
                    ctx.RequesterId = context.RequesterId;
                }, token);
                return;
            }

            if (requestAsPublisher) throw;
            context.Headers.Set(DistributedConfigurators.Headers.DeliveryKindKey,
                DistributedConfigurators.DeliveryKinds.DeadLetter);
            SetExceptionHeaders(context, ex);
            publisher.SetContext(context);
            logger?.LogError(
                "Message: {@Message} processed failed with exception: {@Exception} after {@Times} times. Message has been moved to dead letter queue!",
                context.Message, new { ex.Message, ex.StackTrace }, intervals.Length);
            await publisher.PublishAsync(context.Message, ctx => ctx.RequesterId = context.RequesterId, token);
        }
    }

    private static void SetExceptionHeaders(IConsumerContext context, Exception ex)
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
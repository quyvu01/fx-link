using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Delegates;
using FxLink.Registries;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.InternalPipelineBehaviors;

internal sealed class RetryPipelineBehavior<TMessage>(IServiceProvider serviceProvider)
    : IConsumerPipelineBehavior<TMessage> where TMessage : class
{
    private static readonly ConcurrentDictionary<Type, Func<RetryPipelineBehavior<TMessage>, IMessageRetryPolicy>>
        RetryPolicyDelegateCache = new();

    public async Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        var services = ConsumerAmbient.Services;
        var logger = services.GetService<ILogger<RetryPipelineBehavior<TMessage>>>();
        try
        {
            await next.Invoke(token);
        }
        catch (Exception ex)
        {
            var consumerType = ConsumerAmbient.ConsumerType;
            var retryPolicy = (GetRetryPolicy(consumerType) as MessageRetryPolicy)!;
            var intervals = retryPolicy.RetryIntervals;
            var ignoreExceptions = retryPolicy.IgnoreExceptions;

            var publisher = services.GetService<IPublisher>();
            if (publisher is null) throw;

            if (ShouldIgnore(ex, ignoreExceptions))
            {
                context.Headers[DistributedConfigurators.Headers.MessageTypeKey] =
                    DistributedConfigurators.MessageTypes.DeadLetter;
                context.Headers.Remove(DistributedConfigurators.Headers.TimeToLiveKey);
                context.Headers.Remove(DistributedConfigurators.Headers.RetryCountKey);
                context.Headers[DistributedConfigurators.Headers.ExceptionTypeKey] = ex.GetType().FullName ?? ex.GetType().Name;
                context.Headers[DistributedConfigurators.Headers.ExceptionMessageKey] = ex.Message;
                context.Headers[DistributedConfigurators.Headers.ExceptionStackTraceKey] = ex.StackTrace ?? string.Empty;
                logger?.LogError(
                    "Message has been moved to dead letter queue due the exception ignore: {@Message} with exception: {@Exception}",
                    context.Message, new { ex.Message, ex.StackTrace });
                await publisher.PublishAsync(context.Message, new PublisherContext(context), token);
                return;
            }

            var retryCount = GetRetryCountFromHeader(context.Headers);

            Action headerAction = (retryCount < intervals.Length) switch
            {
                true => () =>
                {
                    var nextRetry = intervals[retryCount];
                    context.Headers[DistributedConfigurators.Headers.RetryCountKey] = retryCount + 1;
                    context.Headers[DistributedConfigurators.Headers.TimeToLiveKey] = nextRetry.TotalMilliseconds;
                    context.Headers[DistributedConfigurators.Headers.MessageTypeKey] =
                        DistributedConfigurators.MessageTypes.Retry;
                    logger?.LogWarning(
                        "Message: {@Message} processed failed with exception: {@Exception}. Retry will be processed after: {@TimeSpan}",
                        context.Message, new { ex.Message, ex.StackTrace }, nextRetry);
                },
                _ => () =>
                {
                    context.Headers[DistributedConfigurators.Headers.MessageTypeKey] =
                        DistributedConfigurators.MessageTypes.DeadLetter;
                    context.Headers.Remove(DistributedConfigurators.Headers.TimeToLiveKey);
                    context.Headers.Remove(DistributedConfigurators.Headers.RetryCountKey);
                    context.Headers[DistributedConfigurators.Headers.ExceptionTypeKey] = ex.GetType().FullName ?? ex.GetType().Name;
                    context.Headers[DistributedConfigurators.Headers.ExceptionMessageKey] = ex.Message;
                    context.Headers[DistributedConfigurators.Headers.ExceptionStackTraceKey] = ex.StackTrace ?? string.Empty;
                    logger?.LogError(
                        "Message: {@Message} processed failed with exception: {@Exception} after {@Times} times. Message has been moved to dead letter queue!",
                        context.Message, new { ex.Message, ex.StackTrace }, intervals.Length);
                }
            };
            headerAction.Invoke();

            await publisher.PublishAsync(context.Message, new PublisherContext(context), token);
        }
    }

    private static bool ShouldIgnore(Exception ex, Type[] ignoreExceptions)
    {
        if (ignoreExceptions is not { Length: > 0 }) return false;
        var exType = ex.GetType();
        return ignoreExceptions.Any(ignored => ignored.IsAssignableFrom(exType));
    }

    private IMessageRetryPolicy GetRetryPolicy(Type consumerType)
    {
        var getter = RetryPolicyDelegateCache
            .GetOrAdd(consumerType, BuildGetRetryPolicyDelegate);
        return getter(this);
    }

    private static Func<RetryPipelineBehavior<TMessage>, IMessageRetryPolicy> BuildGetRetryPolicyDelegate(
        Type consumerType)
    {
        var openMethod = typeof(RetryPipelineBehavior<TMessage>).GetMethod(
            nameof(GetRetryPolicy),
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)!;

        var closedMethod = openMethod.MakeGenericMethod(consumerType);

        var instanceParam = Expression.Parameter(typeof(RetryPipelineBehavior<TMessage>), "instance");
        var call = Expression.Call(instanceParam, closedMethod);

        return Expression.Lambda<Func<RetryPipelineBehavior<TMessage>, IMessageRetryPolicy>>(call, instanceParam)
            .Compile();
    }

    private IMessageRetryPolicy GetRetryPolicy<TConsumer>() where TConsumer : IConsumer
    {
        var retryPolicyForConsumer = serviceProvider.GetService<IConsumerDefinition<TConsumer>>();
        if (retryPolicyForConsumer is { ConsumerConfigurator: ConsumerConfigurator<TConsumer> cForConsumer })
            return !cForConsumer.MessageRetryPolicies.TryGetValue(typeof(TMessage), out var messageRetryPolicy)
                ? cForConsumer.RetryPolicy
                : messageRetryPolicy;

        return MessageRetryPolicy.DefaultMessageRetryPolicy;
    }

    private static int GetRetryCountFromHeader(Dictionary<string, object> headers)
    {
        if (!headers.TryGetValue(DistributedConfigurators.Headers.RetryCountKey, out var retryCountAsObject)) return 0;
        return int.TryParse(JsonSerializer.Serialize(retryCountAsObject), out var retryCount) ? retryCount : 0;
    }
}
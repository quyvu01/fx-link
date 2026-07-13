using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Delegates;
using FxLink.Registries;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.InternalPipelineBehaviors;

internal sealed class RetryPipelineBehavior<TMessage>(IServiceProvider serviceProvider)
    : IConsumerPipelineBehavior<TMessage> where TMessage : class
{
    // Cache is per closed RetryPipelineBehavior<TMessage> (static in generic class = per TMessage instantiation)
    private static readonly ConcurrentDictionary<Type, Func<RetryPipelineBehavior<TMessage>, IMessageRetryPolicy>>
        RetryPolicyDelegateCache = new();

    private static int _retryCount; // Todo: temporary test

    public async Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        var consumerType = ConsumerAmbient.ConsumerType;
        var retryPolicy = (GetRetryPolicy(consumerType) as MessageRetryPolicy)!;
        var intervals = retryPolicy.RetryIntervals;
        var ignoreExceptions = retryPolicy.IgnoreExceptions;

        try
        {
            await next.Invoke(token);
        }
        catch (Exception ex)
        {
            if (ShouldIgnore(ex, ignoreExceptions))
                throw;

            var retryHandler = serviceProvider.GetService<IRetryPolicyHandling<TMessage>>();
            if (retryHandler is null) throw;

            var attempt = context.RetryCount; // Todo: Just for test, need to implement further...
            if (_retryCount++ < intervals.Length)
            {
                await retryHandler.HandleRetryPolicyAsync(context, token);
                return;
            }

            await retryHandler.HandleDeadLetterAsync(context, token);
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
        var getter = RetryPolicyDelegateCache.GetOrAdd(consumerType, BuildGetRetryPolicyDelegate);
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
        var retryPolicyForMessage = serviceProvider.GetService<IAbstractConsumerDefinition<TConsumer, TMessage>>();
        if (retryPolicyForMessage is { ConsumerDefinition: ConsumerDefinition<TConsumer, TMessage> cForMessage })
            return cForMessage.RetryPolicy;

        var retryPolicyForConsumer = serviceProvider.GetService<IAbstractConsumerDefinition<TConsumer>>();
        if (retryPolicyForConsumer is { ConsumerDefinition: ConsumerDefinition<TConsumer> cForConsumer })
            return cForConsumer.RetryPolicy;

        return MessageRetryPolicy.DefaultMessageRetryPolicy;
    }
}
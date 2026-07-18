using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FxLink.Abstractions;
using FxLink.Exceptions;
using FxLink.Registries;

namespace FxLink.Extensions;

public static class ConsumerConfiguratorExtensions
{
    private static readonly ConcurrentDictionary<(Type ConsumerType, Type MessageType), MethodInfo>
        UseMessageRetryMethodCache = new();

    public static void UseMessageRetry<TMessage>(this IConsumerConfigurator configurator,
        [NotNull] Action<IMessageRetryPolicy> options) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(options);
        var configuratorType = configurator.GetType();
        if (!configuratorType.IsGenericType)
            throw new FxLinkException.ConsumerConfiguratorMustBeGeneric(configuratorType);
        var consumerType = configuratorType.GetGenericArguments().First();
        var messageType = typeof(TMessage);
        var method = UseMessageRetryMethodCache.GetOrAdd((consumerType, messageType), BuildUseMessageRetryMethod);
        method.Invoke(null, [configurator, options]);
    }

    private static MethodInfo BuildUseMessageRetryMethod((Type ConsumerType, Type MessageType) key)
    {
        var openMethod = typeof(ConsumerConfiguratorExtensions).GetMethod(
            nameof(UseMessageRetryForConsumerMessage), BindingFlags.NonPublic | BindingFlags.Static)!;
        return openMethod.MakeGenericMethod(key.ConsumerType, key.MessageType);
    }

    private static void UseMessageRetryForConsumerMessage<TConsumer, TMessage>(
        IConsumerConfigurator<TConsumer> configurator, Action<IMessageRetryPolicy> options)
        where TConsumer : IConsumer where TMessage : class
    {
        var messageConfigurator = new ConsumerConfigurator<TConsumer, TMessage>();
        messageConfigurator.UseMessageRetry(options);
        var consumerConfigurator = (ConsumerConfigurator<TConsumer>)configurator;
        consumerConfigurator.MessageRetryPolicies[typeof(TMessage)] = messageConfigurator.RetryPolicy;
    }
}
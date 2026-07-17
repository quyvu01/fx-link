using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;

namespace FxLink.Registries;

public interface IConsumerConfigurator;

public interface IConsumerConfigurator<TConsumer> : IConsumerConfigurator
    where TConsumer : IConsumer
{
    void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options);
}

public interface IConsumerConfigurator<TConsumer, TMessage> : IConsumerConfigurator
    where TConsumer : IConsumer where TMessage : class
{
    void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options);
}
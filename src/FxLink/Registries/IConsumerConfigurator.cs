using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;

namespace FxLink.Registries;

public interface IConsumerConfigurator
{
    void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options);
    void UseMessageRetry<TMessage>([NotNull] Action<IMessageRetryPolicy> options) where TMessage : class;
    void UseBatching<TMessage>([NotNull] Action<IMessageBatchOption<TMessage>> options) where TMessage : class;
}

public interface IConsumerConfigurator<TConsumer> : IConsumerConfigurator
    where TConsumer : IConsumer;
using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;

namespace FxLink.Registries;

public interface IConsumerDefinition
{
    void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options);
}

public interface IConsumerDefinition<TConsumer> : IConsumerDefinition where TConsumer : IConsumer
{
    void UseMessageRetry<TMessage>([NotNull] Action<IMessageRetryPolicy> options) where TMessage : class;
}
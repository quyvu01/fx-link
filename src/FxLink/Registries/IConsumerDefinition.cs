using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;

namespace FxLink.Registries;

public interface IConsumerDefinition;

public interface IConsumerDefinition<TConsumer> : IConsumerDefinition where TConsumer : IConsumer
{
    void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options);
}

public interface IConsumerDefinition<TConsumer, TMessage> : IConsumerDefinition
    where TConsumer : IConsumer where TMessage : class
{
    void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options);
}
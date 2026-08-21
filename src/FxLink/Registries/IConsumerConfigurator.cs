using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;

namespace FxLink.Registries;

public interface IConsumerConfigurator
{
    void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options);
}

public interface IConsumerConfigurator<TConsumer> : IConsumerConfigurator
    where TConsumer : IConsumer;
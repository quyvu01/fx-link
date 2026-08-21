using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;

namespace FxLink.Registries;

internal abstract class AbstractConsumerConfigurator
{
    internal abstract void AddConfigurator(Type targetType, IConsumeConfigurator configurator);

    internal abstract TMessageConfigurator GetConfigurator<TMessageConfigurator>(Type targetType)
        where TMessageConfigurator : IConsumeConfigurator;
}

internal class ConsumerConfigurator<TConsumer> :
    AbstractConsumerConfigurator,
    IConsumerConfigurator<TConsumer> where TConsumer : IConsumer
{
    private readonly ConcurrentDictionary<Type, List<IConsumeConfigurator>> _messageConfigurators = [];

    public void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options)
    {
        var configurator = new ConsumerConfigurator();
        configurator.UseMessageRetry(options);
        AddConfigurator(typeof(TConsumer), configurator.RetryPolicy);
    }

    internal override void AddConfigurator(Type targetType, IConsumeConfigurator configurator)
    {
        var configurators = _messageConfigurators.GetOrAdd(targetType, _ => []);
        configurators.Add(configurator);
    }

    internal override TMessageConfigurator GetConfigurator<TMessageConfigurator>(Type targetType)
    {
        var configurators = _messageConfigurators.GetValueOrDefault(targetType, []);
        return configurators.OfType<TMessageConfigurator>().LastOrDefault();
    }
}

internal class ConsumerConfigurator
{
    internal IMessageRetryPolicy RetryPolicy { get; private set; } = MessageRetryPolicy.DefaultMessageRetryPolicy;

    public void UseMessageRetry([NotNull] Action<IMessageRetryPolicy> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var retryPolicy = new MessageRetryPolicy();
        options.Invoke(retryPolicy);
        RetryPolicy = retryPolicy;
    }
}
using System.Collections.Concurrent;
using FxLink.Abstractions;

namespace FxLink.Registries;

internal class ConsumerConfigurator<TConsumer> :
    IMessageConfiguratorBehavior,
    IConsumerConfigurator<TConsumer> where TConsumer : IConsumer
{
    private readonly ConcurrentDictionary<Type, List<IMessageConfigurator>> _messageConfigurators = [];

    public void UseMessageRetry(Action<IMessageRetryPolicy> options)
    {
        var retryPolicy = new MessageRetryPolicy();
        options?.Invoke(retryPolicy);
        AddConfigurator(typeof(TConsumer), retryPolicy);
    }

    public void AddConfigurator(Type targetType, IMessageConfigurator configurator)
    {
        var configurators = _messageConfigurators.GetOrAdd(targetType, _ => []);
        configurators.Add(configurator);
    }

    public IMessageConfigurator[] GetConfigurators(Type targetType) =>
        [.. _messageConfigurators.GetValueOrDefault(targetType, [])];

    public TMessageConfigurator GetConfigurator<TMessageConfigurator>(Type targetType)
        where TMessageConfigurator : IMessageConfigurator
    {
        var configurators = _messageConfigurators.GetValueOrDefault(targetType, []);
        return configurators.OfType<TMessageConfigurator>().LastOrDefault();
    }
}

internal class ConsumerConfigurator<TConsumer, TMessage> :
    IConsumerConfigurator<TConsumer, TMessage>
    where TConsumer : IConsumer where TMessage : class
{
    private IMessageRetryPolicy _retryPolicyForMessage;

    internal IMessageRetryPolicy RetryPolicy =>
        _retryPolicyForMessage ?? MessageRetryPolicy.DefaultMessageRetryPolicy;

    public void UseMessageRetry(Action<IMessageRetryPolicy> options)
    {
        var retryPolicy = new MessageRetryPolicy();
        options?.Invoke(retryPolicy);
        _retryPolicyForMessage = retryPolicy;
    }
}
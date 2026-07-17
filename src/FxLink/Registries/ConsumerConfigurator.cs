using FxLink.Abstractions;

namespace FxLink.Registries;

internal class ConsumerConfigurator<TConsumer> : IConsumerConfigurator<TConsumer> where TConsumer : IConsumer
{
    private IMessageRetryPolicy _retryPolicyForConsumer;

    internal IMessageRetryPolicy RetryPolicy => _retryPolicyForConsumer ?? MessageRetryPolicy.DefaultMessageRetryPolicy;
    internal readonly Dictionary<Type, IMessageRetryPolicy> MessageRetryPolicies = [];
    public void UseMessageRetry(Action<IMessageRetryPolicy> options)
    {
        var retryPolicy = new MessageRetryPolicy();
        options?.Invoke(retryPolicy);
        _retryPolicyForConsumer = retryPolicy;
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
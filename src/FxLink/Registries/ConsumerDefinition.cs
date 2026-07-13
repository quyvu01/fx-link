using FxLink.Abstractions;

namespace FxLink.Registries;

internal class ConsumerDefinition<TConsumer> : IConsumerDefinition<TConsumer> where TConsumer : IConsumer
{
    private IMessageRetryPolicy _retryPolicyForConsumer;

    internal IMessageRetryPolicy RetryPolicy => _retryPolicyForConsumer ?? MessageRetryPolicy.DefaultMessageRetryPolicy;

    public void UseMessageRetry(Action<IMessageRetryPolicy> options)
    {
        var retryPolicy = new MessageRetryPolicy();
        options?.Invoke(retryPolicy);
        _retryPolicyForConsumer = retryPolicy;
    }
}

internal class ConsumerDefinition<TConsumer, TMessage> : IConsumerDefinition<TConsumer, TMessage>
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
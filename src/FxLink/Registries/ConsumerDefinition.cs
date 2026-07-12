using FxLink.Abstractions;

namespace FxLink.Registries;

internal class ConsumerDefinition<TConsumer> : IConsumerDefinition<TConsumer> where TConsumer : IConsumer
{
    // Temporary, not enough
    internal List<IMessageRetryPolicy> Policies { get; } = [];

    public void UseMessageRetry<TMessage>(Action<IMessageRetryPolicy> options) where TMessage : class
    {
        var retryPolicy = new MessageRetryPolicy();
        options?.Invoke(retryPolicy);
        Policies.Add(retryPolicy);
    }

    public void UseMessageRetry(Action<IMessageRetryPolicy> options)
    {
        var retryPolicy = new MessageRetryPolicy();
        options?.Invoke(retryPolicy);
        Policies.Add(retryPolicy);
    }
}
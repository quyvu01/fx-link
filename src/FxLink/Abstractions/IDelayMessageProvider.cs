using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

/// <summary>
/// Delivers a message only after a delay has elapsed. Each transport ships its own default
/// implementation (e.g. FxLink.RabbitMq's delayed-message-exchange plugin, registered via
/// UseRabbitMqDelayScheduler()); register a different implementation to back delays with
/// Quartz, Hangfire, Redis, etc. instead. No default is registered unless a transport's
/// "Use...DelayScheduler()" extension is called.
/// </summary>
public interface IDelayMessageProvider
{
    Task PublishDelayedAsync<TMessage>(TMessage message, IContext context, long delayInMs,
        CancellationToken cancellationToken = default) where TMessage : class;
}

using RabbitMQ.Client;

namespace FxLink.RabbitMq.Abstractions;

/// <summary>
/// Optional capability an <see cref="FxLink.Abstractions.IDelayMessageProvider"/> implementation can
/// also provide when it needs RabbitMQ broker topology (exchanges/bindings) declared up front.
/// Non-RabbitMQ providers (Quartz, Hangfire, Redis, ...) simply don't implement this, and
/// RabbitMqClient skips topology declaration for delay entirely.
/// </summary>
internal interface IRabbitMqDelayTopology
{
    Task DeclareTopologyAsync(IChannel channel, Type messageType, string queueName,
        CancellationToken cancellationToken = default);
}
using FxLink.RabbitMq.Implementations;

namespace FxLink.RabbitMq.Registries;

public interface IRabbitMqConfigurator
{
    void Host(string host, string virtualHost, int port = 5672, Action<RabbitMqCredential> configure = null);

    /// <summary>
    /// Sets the size of the bounded publishing channel pool used by <see cref="RabbitMqClient"/>.
    /// Defaults to min(ProcessorCount, 8) when not configured.
    /// </summary>
    void PublishChannelPoolSize(int poolSize);

    /// <summary>
    /// Declares queues, exchanges, retry and dead-letter topology as durable and publishes messages as
    /// persistent, so they survive a broker restart. Defaults to <c>false</c> (transient topology).
    /// Switching an existing deployment requires deleting or renaming its already-declared queues and
    /// exchanges, because RabbitMQ rejects re-declaring them with a different durability.
    /// The per-instance reply queue stays exclusive and non-durable regardless.
    /// </summary>
    void Durable(bool durable);

    void PrefetchCount(ushort count);
    void ConcurrentMessageLimit(ushort limitCount);
}
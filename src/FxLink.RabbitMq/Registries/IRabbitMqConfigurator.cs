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

    /// <summary>
    /// Controls what happens to a delivery when an exception escapes the consumer pipeline — i.e. the
    /// retry/dead-letter mechanism itself failed (republish failed, Inbox store unreachable, ...), not a
    /// consumer exception, which <c>RetryPipelineBehavior</c> already handles. By default such a delivery
    /// is acked and only logged, so it is lost. When configured, it is instead negatively acknowledged
    /// with requeue after waiting <paramref name="delay"/>, so it is redelivered once the dependency
    /// recovers. The wait holds the consumer's dispatch slot, which also throttles a tight redelivery
    /// loop while the failure lasts.
    /// </summary>
    void RequeueOnFailure(TimeSpan delay);

    void PrefetchCount(ushort count);
    void ConcurrentMessageLimit(ushort limitCount);
}
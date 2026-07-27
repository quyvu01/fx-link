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

    void PrefetchCount(ushort count);
    void ConcurrentMessageLimit(ushort limitCount);
}
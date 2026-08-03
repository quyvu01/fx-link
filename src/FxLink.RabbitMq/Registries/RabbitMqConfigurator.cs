using FxLink.RabbitMq.Constants;

namespace FxLink.RabbitMq.Registries;

internal sealed class RabbitMqConfigurator : IRabbitMqConfigurator
{
    private string _hostValue;
    private string _virtualHostValue;
    private int _portValue = RabbitMqConstants.DefaultPort;
    private RabbitMqCredential Credential { get; } = new();
    private int _poolSizeValue = RabbitMqConstants.DefaultPoolSize;
    private ushort _prefetchCountValue = RabbitMqConstants.DefaultPrefetchCount;
    private ushort _concurrentMessageLimitValue = RabbitMqConstants.DefaultConcurrentMessageLimit;

    public void Host(string host, string virtualHost, int port = 5672, Action<RabbitMqCredential> configure = null)
    {
        _hostValue = host;
        _virtualHostValue = virtualHost;
        _portValue = port;
        configure?.Invoke(Credential);
    }

    public void PublishChannelPoolSize(int poolSize)
    {
        if (poolSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(poolSize), poolSize,
                "Publish channel pool size must be greater than zero.");
        _poolSizeValue = poolSize;
    }

    public void PrefetchCount(ushort prefetchCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(prefetchCount, 0);
        _prefetchCountValue = prefetchCount;
    }

    public void ConcurrentMessageLimit(ushort limitCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limitCount, 0);
        _concurrentMessageLimitValue = limitCount;
    }

    internal IRabbitMqConfiguration ToConfiguration() => new RabbitMqConfiguration(_hostValue, _virtualHostValue,
        _portValue, Credential.UserNameValue, Credential.PasswordValue, Credential.SslOptionValue, _poolSizeValue,
        _prefetchCountValue, _concurrentMessageLimitValue);
}
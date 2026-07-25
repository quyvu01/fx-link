namespace FxLink.RabbitMq.Registries;

internal sealed class RabbitMqConfigurator : IRabbitMqConfigurator
{
    private string _hostValue;
    private string _virtualHostValue;
    private int _portValue = 5672;
    private RabbitMqCredential Credential { get; } = new();
    private int _poolSizeValue = Math.Min(Environment.ProcessorCount, 8);
    private ushort _prefetchCount = (ushort)Math.Min(Environment.ProcessorCount * 2, 16);

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

    public void PrefetchCount(ushort count) => _prefetchCount = count;

    internal IRabbitMqConfiguration ToConfiguration() => new RabbitMqConfiguration(_hostValue, _virtualHostValue,
        _portValue, Credential.UserNameValue, Credential.PasswordValue, Credential.SslOptionValue, _poolSizeValue,
        _prefetchCount);
}
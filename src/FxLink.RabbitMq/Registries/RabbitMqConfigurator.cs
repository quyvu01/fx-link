using FxLink.RabbitMq.Constants;
using RabbitMQ.Client;

namespace FxLink.RabbitMq.Registries;

internal sealed class RabbitMqConfigurator : IRabbitMqConfigurator
{
    private string _hostValue;
    private string _virtualHostValue;
    private int? _portValue;
    private RabbitMqCredential Credential { get; } = new();
    private int _poolSizeValue = RabbitMqConstants.DefaultPoolSize;
    private ushort _prefetchCountValue = RabbitMqConstants.DefaultPrefetchCount;
    private ushort _concurrentMessageLimitValue = RabbitMqConstants.DefaultConcurrentMessageLimit;
    private bool _durableValue;
    private TimeSpan? _requeueOnFailureDelayValue;
    private Action<ConnectionFactory> _configureConnectionValue;

    public void Host(string host, string virtualHost, int? port = null, Action<RabbitMqCredential> configure = null)
    {
        _hostValue = host;
        _virtualHostValue = virtualHost;
        _portValue = port;
        configure?.Invoke(Credential);
    }

    public void ConfigureConnection(Action<ConnectionFactory> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureConnectionValue += configure;
    }

    public void PublishChannelPoolSize(int poolSize)
    {
        if (poolSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(poolSize), poolSize,
                "Publish channel pool size must be greater than zero.");
        _poolSizeValue = poolSize;
    }

    public void Durable(bool durable) => _durableValue = durable;

    public void RequeueOnFailure(TimeSpan delay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        _requeueOnFailureDelayValue = delay;
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

    internal IRabbitMqConfiguration ToConfiguration()
    {
        var sslOption = Credential.SslOptionValue;
        // The certificate is validated against ServerName — left empty, the handshake fails with a name
        // mismatch even though the broker is the one named in Host(...).
        if (sslOption is { Enabled: true } && string.IsNullOrEmpty(sslOption.ServerName))
            sslOption.ServerName = _hostValue;

        var port = _portValue ??
                   (sslOption is { Enabled: true } ? RabbitMqConstants.DefaultSslPort : RabbitMqConstants.DefaultPort);

        return new RabbitMqConfiguration(_hostValue, _virtualHostValue, port, Credential.UserNameValue,
            Credential.PasswordValue, sslOption, _poolSizeValue, _prefetchCountValue, _concurrentMessageLimitValue,
            _durableValue, _requeueOnFailureDelayValue, _configureConnectionValue);
    }
}
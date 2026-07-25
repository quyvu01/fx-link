using FxLink.RabbitMq.Abstractions;

namespace FxLink.RabbitMq.Registries;

internal sealed class RabbitMqConfigurator : IRabbitMqConfigurator
{
    public string HostValue { get; set; }
    public string VirtualHostValue { get; set; }
    public int PortValue { get; set; } = 5672;
    public RabbitMqCredential Credential { get; } = new();
    public int PoolSizeValue { get; set; } = Math.Min(Environment.ProcessorCount, 8);

    public void Host(string host, string virtualHost, int port = 5672, Action<RabbitMqCredential> configure = null)
    {
        HostValue = host;
        VirtualHostValue = virtualHost;
        PortValue = port;
        configure?.Invoke(Credential);
    }

    public void PublishChannelPoolSize(int poolSize)
    {
        if (poolSize <= 0) throw new ArgumentOutOfRangeException(nameof(poolSize), poolSize,
            "Publish channel pool size must be greater than zero.");
        PoolSizeValue = poolSize;
    }

    internal IRabbitMqConfiguration ToConfiguration() => new RabbitMqConfiguration(HostValue, VirtualHostValue,
        PortValue, Credential.UserNameValue, Credential.PasswordValue, Credential.SslOptionValue, PoolSizeValue);
}
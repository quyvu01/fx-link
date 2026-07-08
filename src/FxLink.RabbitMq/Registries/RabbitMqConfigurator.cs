using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Implementations;

namespace FxLink.RabbitMq.Registries;

internal sealed class RabbitMqConfigurator : IRabbitMqConfigurator
{
    public string HostValue { get; set; }
    public string VirtualHostValue { get; set; }
    public int PortValue { get; set; } = 5672;
    public RabbitMqCredential Credential { get; } = new();

    public void Host(string host, string virtualHost, int port = 5672, Action<RabbitMqCredential> configure = null)
    {
        HostValue = host;
        VirtualHostValue = virtualHost;
        PortValue = port;
        configure?.Invoke(Credential);
    }

    internal IRabbitMqConfiguration ToConfiguration() => new RabbitMqConfiguration(HostValue, VirtualHostValue,
        PortValue, Credential.UserNameValue, Credential.PasswordValue, Credential.SslOptionValue);
}
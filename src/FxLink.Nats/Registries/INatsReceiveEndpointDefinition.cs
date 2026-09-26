using FxLink.Registries;

namespace FxLink.Nats.Registries;

public interface INatsReceiveEndpointDefinition : IOption
{
    string ReceiveEndpoint { get; }
}

internal sealed class NatsReceiveEndpointDefinition : INatsReceiveEndpointDefinition
{
    public string ReceiveEndpoint { get; private set; }
    internal void SetReceiveEndpoint(string receiveEndpoint) => ReceiveEndpoint = receiveEndpoint;
}

using FxLink.Registries;

namespace FxLink.Azure.ServiceBus.Registries;

public interface IServiceBusReceiveEndpointDefinition : IOption
{
    string ReceiveEndpoint { get; }
}

internal sealed class ServiceBusReceiveEndpointDefinition : IServiceBusReceiveEndpointDefinition
{
    public string ReceiveEndpoint { get; private set; }
    internal void SetReceiveEndpoint(string receiveEndpoint) => ReceiveEndpoint = receiveEndpoint;
}

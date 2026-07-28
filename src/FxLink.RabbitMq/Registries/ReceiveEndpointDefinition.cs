namespace FxLink.RabbitMq.Registries;

internal sealed class ReceiveEndpointDefinition : IReceiveEndpointDefinition
{
    public string ReceiveEndpoint { get; private set; }
    public bool AutoDelete { get; private set; }
    internal void SetReceivedEndpoint(string receiveEndpoint) => ReceiveEndpoint = receiveEndpoint;
    internal void SetAutoDelete(bool autoDelete) => AutoDelete = autoDelete;
}
namespace FxLink.RabbitMq.Registries;

internal class ReceivedEndpointConfigurator : IReceivedEndpointConfigurator
{
    public bool AutoDelete { get; set; }
}
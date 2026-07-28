namespace FxLink.RabbitMq.Registries;

public interface IReceivedEndpointConfigurator
{
    bool AutoDelete { set; }
}
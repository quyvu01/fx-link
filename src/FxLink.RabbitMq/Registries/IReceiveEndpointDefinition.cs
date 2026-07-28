using FxLink.Registries;

namespace FxLink.RabbitMq.Registries;

public interface IReceiveEndpointDefinition : IMessageConfigurator
{
    string ReceiveEndpoint { get; }
    bool AutoDelete { get; }
}
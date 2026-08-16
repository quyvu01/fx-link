using FxLink.Registries;

namespace FxLink.RabbitMq.Registries;

public interface IReceiveEndpointDefinition : IConsumeConfigurator
{
    string ReceiveEndpoint { get; }
    bool AutoDelete { get; }
}
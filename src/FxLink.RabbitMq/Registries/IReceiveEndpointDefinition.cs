using FxLink.Registries;

namespace FxLink.RabbitMq.Registries;

public interface IReceiveEndpointDefinition : IOption
{
    string ReceiveEndpoint { get; }
    bool AutoDelete { get; }
}
using FxLink.Registries;

namespace FxLink.RabbitMq.Registries;

internal interface IConsumerDispatchDefinition : IMessageConfigurator
{
    public ushort PrefetchCount { get; }
    public ushort ConcurrentMessageLimit { get; }
}

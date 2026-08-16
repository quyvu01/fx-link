using FxLink.Registries;

namespace FxLink.RabbitMq.Registries;

internal interface IConsumerDispatchDefinition : IConsumeConfigurator
{
    public ushort PrefetchCount { get; }
    public ushort ConcurrentMessageLimit { get; }
}

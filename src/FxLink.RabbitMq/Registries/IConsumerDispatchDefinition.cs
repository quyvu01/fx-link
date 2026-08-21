using FxLink.Registries;

namespace FxLink.RabbitMq.Registries;

internal interface IConsumerDispatchDefinition : IOption
{
    public ushort PrefetchCount { get; }
    public ushort ConcurrentMessageLimit { get; }
}

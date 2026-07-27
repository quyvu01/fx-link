namespace FxLink.RabbitMq.Registries;

internal class ConsumerDispatchDefinition : IConsumerDispatchDefinition
{
    public ushort PrefetchCount { get; private set; }
    public ushort ConcurrentMessageLimit { get; private set; }

    internal void SetPrefetchCount(ushort prefetchCount) => PrefetchCount = prefetchCount;
    internal void SetConcurrentMessageLimit(ushort limitCount) => ConcurrentMessageLimit = limitCount;

    internal static IConsumerDispatchDefinition FromConfiguration(IRabbitMqConfiguration configuration) =>
        new ConsumerDispatchDefinition
        {
            PrefetchCount = configuration.PrefetchCount,
            ConcurrentMessageLimit = configuration.ConcurrentMessageLimit
        };
}

using FxLink.RabbitMq.Constants;

namespace FxLink.RabbitMq.Registries;

internal class ConsumerDispatchDefinition : IConsumerDispatchDefinition
{
    private ushort? _prefetchCount;
    private ushort? _concurrentMessageLimit;

    public ushort PrefetchCount
    {
        get => _prefetchCount ?? RabbitMqConstants.DefaultPrefetchCount;
        private init => _prefetchCount = value;
    }

    public ushort ConcurrentMessageLimit
    {
        get => _concurrentMessageLimit ?? _prefetchCount ?? RabbitMqConstants.DefaultConcurrentMessageLimit;
        private init => _concurrentMessageLimit = value;
    }

    internal void SetPrefetchCount(ushort prefetchCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(prefetchCount, 1);
        _prefetchCount = prefetchCount;
    }

    internal void SetConcurrentMessageLimit(ushort concurrentMessageLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrentMessageLimit, 1);
        _concurrentMessageLimit = concurrentMessageLimit;
    }

    internal static IConsumerDispatchDefinition FromConfiguration(IRabbitMqConfiguration configuration) =>
        new ConsumerDispatchDefinition
        {
            PrefetchCount = configuration.PrefetchCount,
            ConcurrentMessageLimit = configuration.ConcurrentMessageLimit
        };
}
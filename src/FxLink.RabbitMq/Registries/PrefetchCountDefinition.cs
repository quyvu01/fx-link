namespace FxLink.RabbitMq.Registries;

internal class PrefetchCountDefinition(ushort prefetchCount) : IPrefetchCountDefinition
{
    public ushort PrefetchCount { get; } = prefetchCount;
}
using FxLink.Registries;

namespace FxLink.RabbitMq.Registries;

internal interface IPrefetchCountDefinition : IMessageConfigurator
{
    public ushort PrefetchCount { get; }
}
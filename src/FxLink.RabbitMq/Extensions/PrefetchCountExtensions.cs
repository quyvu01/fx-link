using FxLink.Abstractions;
using FxLink.RabbitMq.Registries;
using FxLink.Registries;

namespace FxLink.RabbitMq.Extensions;

public static class PrefetchCountExtensions
{
    public static void PrefetchCount<TConsumer>(this IConsumerConfigurator<TConsumer> configurator, ushort count)
        where TConsumer : IConsumer
    {
        var consumerConfigurator = (ConsumerConfigurator<TConsumer>)configurator;
        var prefetchCountDefinition = new PrefetchCountDefinition(count);
        consumerConfigurator.AddConfigurator(typeof(TConsumer), prefetchCountDefinition);
    }
}
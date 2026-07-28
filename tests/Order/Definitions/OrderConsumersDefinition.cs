using FxLink.Abstractions;
using FxLink.RabbitMq.Extensions;
using FxLink.Registries;
using Order.Consumers;

namespace Order.Definitions;

public sealed class OrderConsumersDefinition : ConsumerDefinition<OrderConsumers>
{
    public override void Configure(IConsumerConfigurator<OrderConsumers> options)
    {
        options.ReceivedEndpoint("some-consumer", c =>
        {
            c.AutoDelete = true;
        });
    }
}
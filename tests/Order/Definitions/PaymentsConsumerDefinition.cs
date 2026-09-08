using FxLink.Abstractions;
using FxLink.Registries;
using Order.Consumers;
using Order.Exceptions;

namespace Order.Definitions;

public sealed class PaymentsConsumerDefinition : ConsumerDefinition<PaymentsConsumer>
{
    public override void Configure(IConsumerConfigurator<PaymentsConsumer> options)
    {
        options.UseMessageRetry(c =>
        {
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
            c.Ignore<ChildInvalidDataException>();
        });
    }
}
using FxLink.Abstractions;
using FxLink.Registries;
using Order.Consumers;
using Order.Dtos.Batches;

namespace Order.Definitions;

public sealed class TestBatchDefinition : ConsumerDefinition<TestBatchConsumer>
{
    public override void Configure(IConsumerConfigurator<TestBatchConsumer> options)
    {
        options.UseBatching<IInventoryCreated>(c => c
            .GroupBy(x => x.Message.Name)
            .SetMessageLimit(10)
            .SetTimeLimit(TimeSpan.FromSeconds(5))
            .SetConcurrencyLimit(2)
        );
    }
}
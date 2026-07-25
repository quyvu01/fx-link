using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.RabbitMq.Extensions;
using FxLink.Registries;
using StateMachine.Dtos.Inventory;
using StateMachine.StateMachines.Inventory;

namespace StateMachine.ConsumerDefinitions;

public class InventoryReservationStateMachineDefinition : ConsumerDefinition<InventoryReservationStateMachine>
{
    public override void Configure(IConsumerConfigurator<InventoryReservationStateMachine> options)
    {
        options.UseMessageRetry(c =>
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        options.UseMessageRetry<ReserveInventory>(c => c
            .Intervals(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));
        options.PrefetchCount(10);
    }
}
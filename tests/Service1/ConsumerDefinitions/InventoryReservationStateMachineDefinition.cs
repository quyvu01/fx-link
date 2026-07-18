using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.Registries;
using Service1.Dtos.Inventory;
using Service1.StateMachines.Inventory;

namespace Service1.ConsumerDefinitions;

public class InventoryReservationStateMachineDefinition : ConsumerDefinition<InventoryReservationStateMachine>
{
    public override void Configure(IConsumerConfigurator<InventoryReservationStateMachine> options)
    {
        options.UseMessageRetry(c =>
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        options.UseMessageRetry<ReserveInventory>(c => c
            .Intervals(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));
    }
}
using FxLink.StateMachine.Abstractions;

namespace Service1.StateMachines.Inventory;

// No RowVersion/concurrency token here: under Pessimistic mode, isolation is provided by the
// per-correlation-id advisory lock instead of an EF concurrency token.
public sealed class InventoryReservationInstance : IStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string State { get; set; }
    public Guid OrderId { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public DateTime ReservedAt { get; set; }
    public Guid? TokenId { get; set; }
}

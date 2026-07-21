namespace StateMachine.Dtos.Inventory;

// Published (not consumed within this sample) when a reservation is released, whether by direct
// release or because a warehouse stock check came back negative.
public sealed class InventoryReleased
{
    public Guid OrderId { get; set; }
}

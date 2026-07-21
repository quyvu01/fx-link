namespace StateMachine.Dtos.Inventory;

public sealed class AdjustStock
{
    public Guid OrderId { get; set; }
    public int NewQuantity { get; set; }
}

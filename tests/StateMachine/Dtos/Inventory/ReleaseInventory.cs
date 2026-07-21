namespace StateMachine.Dtos.Inventory;

public sealed class ReleaseInventory
{
    public Guid OrderId { get; set; }
    public string Sku { get; set; }

    // >= the reservation's Quantity releases it entirely (IfElse -> Complete); less than that is a
    // partial release that just decrements the held quantity.
    public int Quantity { get; set; }
}

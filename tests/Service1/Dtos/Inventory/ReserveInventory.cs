namespace Service1.Dtos.Inventory;

public sealed class ReserveInventory
{
    public Guid OrderId { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }
}

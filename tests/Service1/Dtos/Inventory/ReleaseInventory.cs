namespace Service1.Dtos.Inventory;

public sealed class ReleaseInventory
{
    public Guid OrderId { get; set; }
    public string Sku { get; set; }
}

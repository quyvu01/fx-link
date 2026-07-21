namespace StateMachine.Dtos.Inventory;

// Request/RequestAsync demo: the state machine asks a plain consumer (WarehouseConsumer) for stock
// status. Sku == "OUT_OF_STOCK" answers false; Sku == "FAIL" makes the consumer throw, exercising
// the Request(...).Failed path.
public sealed class CheckWarehouseStock
{
    public Guid OrderId { get; set; }
    public string Sku { get; set; }
}

public sealed class WarehouseStockResponse
{
    public Guid OrderId { get; set; }
    public bool InStock { get; set; }
}

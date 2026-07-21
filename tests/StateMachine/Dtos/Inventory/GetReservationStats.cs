namespace StateMachine.Dtos.Inventory;

public sealed class GetReservationStats
{
    public Guid OrderId { get; set; }
}

public sealed class ReservationStatsResponse
{
    public Guid OrderId { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public string State { get; set; }
}

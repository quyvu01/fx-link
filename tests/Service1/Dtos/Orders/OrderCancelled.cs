namespace Service1.Dtos.Orders;

public sealed class OrderCancelled
{
    public Guid OrderId { get; set; }
    public DateTime CancelledTime { get; set; }
}

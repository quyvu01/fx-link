namespace Service1.Dtos;

public class OrderCancelled
{
    public Guid OrderId { get; set; }
    public DateTime CancelledTime { get; set; }
}
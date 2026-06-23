namespace Service1.StateMachines.Events;

public class OrderCancelled
{
    public Guid OrderId { get; set; }
    public DateTime CancelledTime { get; set; }
}
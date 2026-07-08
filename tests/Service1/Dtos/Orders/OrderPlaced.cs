namespace Service1.Dtos.Orders;

// Plain pub/sub message, not tied to any state machine. Demonstrates IPublisherContext.Delay
// (deferred publish) and both consumer/publisher pipeline behaviors.
public sealed class OrderPlaced
{
    public Guid OrderId { get; set; }
    public DateTime OrderTime { get; set; }
}

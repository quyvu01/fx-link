namespace Order.Dtos.Orders;

// Plain pub/sub message, not tied to any state machine: IPublisher.PublishAsync -> IConsumer<OrderPlaced>.
public sealed class OrderPlaced
{
    public Guid OrderId { get; set; }
    public DateTime OrderTime { get; set; }
}

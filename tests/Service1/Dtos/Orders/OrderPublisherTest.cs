namespace Service1.Dtos.Orders;

// Plain pub/sub message consumed outside of any state machine flow.
public sealed class OrderPublisherTest
{
    public Guid OrderId { get; set; }
    public string Result => "Just for test!";
}

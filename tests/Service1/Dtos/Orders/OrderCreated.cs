namespace Service1.Dtos.Orders;

public sealed class OrderCreated
{
    public Guid OrderId { get; set; }
    public string OrderName { get; set; }

    // > 3 sends the order down the "succeed" branch, < 0 forces the OrderHistory request
    // (raised while OrderInRequesting) to fail so the Request(...).Failed path can be exercised.
    public int RandomNumber { get; set; }
}

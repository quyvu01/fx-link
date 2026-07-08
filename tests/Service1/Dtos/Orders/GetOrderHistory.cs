namespace Service1.Dtos.Orders;

public sealed class GetOrderHistory
{
    public Guid OrderId { get; set; }

    // Set from OrderCreated.RandomNumber < 0 to deliberately fail the request, exercising the
    // Request(...).Failed path (Fault<GetOrderHistory>).
    public bool ForceFail { get; set; }
}

public sealed class OrderHistoryResponse
{
    public Guid OrderId { get; set; }
    public string Historical { get; set; }
}

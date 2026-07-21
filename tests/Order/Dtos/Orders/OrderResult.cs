namespace Order.Dtos.Orders;

// Raw request/reply demo via IRequester<T> directly (no state machine involved).
public sealed class OrderResult
{
    public Guid OrderId { get; set; }
}

public sealed class OrderResultResponse
{
    public Guid OrderId { get; set; }
    public bool IsSucceed { get; set; }
}

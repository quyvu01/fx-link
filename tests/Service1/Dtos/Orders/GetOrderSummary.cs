namespace Service1.Dtos.Orders;

// Bound via DuringAny(...) on OrderStateMachine, so it can be asked in any known state.
public sealed class GetOrderSummary
{
    public Guid OrderId { get; set; }
}

public sealed class OrderSummaryResponse
{
    public Guid OrderId { get; set; }
    public string State { get; set; }
    public DateTime SummarizedAt { get; set; }
}

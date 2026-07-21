namespace Contracts.Messages;

// Cross-service request/reply: Order -> Payment.
public sealed class ChargePayment
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class PaymentResult
{
    public Guid OrderId { get; set; }
    public bool Success { get; set; }
    public Guid? TransactionId { get; set; }
}

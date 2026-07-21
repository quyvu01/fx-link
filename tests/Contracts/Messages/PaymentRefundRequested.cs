namespace Contracts.Messages;

// Cross-service plain pub/sub: Order -> Payment (fire and forget, no response expected).
public sealed class PaymentRefundRequested
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}

namespace Contracts.Payments;

public interface IPaymentCreated
{
    string PaymentNumber { get; }
    int RandomNumber { get; }
}

public sealed class PaymentCreatedTest
{
    public string PaymentNumber { get; set; }
    public int RandomNumber { get; set; }
}
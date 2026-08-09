using Contracts.Messages;
using FxLink.Abstractions;
using FxLink.Contexts;

namespace Payment.Consumers;

public sealed class PaymentConsumers(ILogger<PaymentConsumers> logger) :
    IConsumer<ChargePayment>,
    IConsumer<PaymentRefundRequested>
{
    // Cross-service request/reply: Order -> Payment. Amount <= 0 throws, which exercises the
    // default retry policy (see ConsumerDefinition<T>) and eventually the dead-letter queue -
    // Order's IRequester<ChargePayment> call then simply times out.
    public async Task ConsumeAsync(IConsumerContext<ChargePayment> context, CancellationToken token = default)
    {
        logger.LogInformation("Charging payment: {@Message}", context.Message);
        await Task.Delay(TimeSpan.FromMilliseconds(300), token);
        if (context.Message.Amount <= 0)
            throw new InvalidOperationException(
                $"Invalid charge amount {context.Message.Amount} for order {context.Message.OrderId}");

        await context.ResponseAsync(new PaymentResult
        {
            OrderId = context.Message.OrderId,
            Success = true,
            TransactionId = Guid.NewGuid()
        }, token);
    }

    // Cross-service plain pub/sub: Order -> Payment, fire and forget.
    public Task ConsumeAsync(IConsumerContext<PaymentRefundRequested> context, CancellationToken token = default)
    {
        logger.LogInformation("Refund requested: {@Message}", context.Message);
        return Task.CompletedTask;
    }
}

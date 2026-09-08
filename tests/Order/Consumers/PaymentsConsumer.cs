using Contracts.Payments;
using FxLink.Abstractions;
using FxLink.Contexts;
using Order.Exceptions;

namespace Order.Consumers;

public sealed class PaymentsConsumer(ILogger<PaymentsConsumer> logger) : IConsumer<IPaymentCreated>
{
    public async Task ConsumeAsync(IConsumeContext<IPaymentCreated> context, CancellationToken token = default)
    {
        await Task.Yield();
        logger.LogInformation("Received message: {@Msg}", context.Message);
        if (context.Message.RandomNumber == 0)
            throw new ChildChildException();
        if(context.Message.RandomNumber == 1)
            throw new Exception("Some exception");
    }
}
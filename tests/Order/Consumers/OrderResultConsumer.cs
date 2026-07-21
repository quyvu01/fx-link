using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Order.Dtos.Orders;

namespace Order.Consumers;

// Raw request/reply demo: no state machine involved, just IRequester<T> -> IConsumer<T>.ResponseAsync.
public sealed class OrderResultConsumer(ILogger<OrderResultConsumer> logger) : IConsumer<OrderResult>
{
    public async Task ConsumeAsync(IConsumerContext<OrderResult> context, CancellationToken token = default)
    {
        logger.LogInformation("Order result request: {@OrderRequest}", context.Message);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await context.ResponseAsync(new OrderResultResponse { OrderId = context.Message.OrderId, IsSucceed = true },
            token);
    }
}

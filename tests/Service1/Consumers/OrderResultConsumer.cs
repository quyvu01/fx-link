using FxLink.Core.Abstractions;
using Service1.Dtos;

namespace Service1.Consumers;

public sealed class OrderResultConsumer(ILogger<OrderResultConsumer> logger) : IConsumer<OrderResult>
{
    public async Task ConsumeAsync(IConsumerContext<OrderResult> context, CancellationToken token = default)
    {
        logger.LogInformation("Order result request: {@OrderRequest}", context.Message);
        await context.ResponseAsync(new OrderResultResponse { OrderId = context.Message.OrderId, IsSucceed = true },
            token);
    }
}
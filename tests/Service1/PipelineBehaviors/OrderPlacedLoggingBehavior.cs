using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Delegates;
using Service1.Dtos.Orders;

namespace Service1.PipelineBehaviors;

public sealed class OrderPlacedLoggingBehavior(ILogger<OrderPlacedLoggingBehavior> logger) :
    IConsumerPipelineBehavior<OrderPlaced>
{
    public async Task ConsumeAsync(IConsumerContext<OrderPlaced> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        logger.LogInformation("Consumer pipeline behavior for OrderPlaced: {@Message}", context.Message);
        await next.Invoke(token);
    }
}

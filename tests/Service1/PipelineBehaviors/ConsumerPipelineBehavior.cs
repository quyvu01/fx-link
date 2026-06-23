using FxLink.Abstractions;
using FxLink.Delegates;
using Service1.Dtos;

namespace Service1.PipelineBehaviors;

public sealed class ConsumerPipelineBehavior(ILogger<ConsumerPipelineBehavior> logger) :
    IConsumerPipelineBehavior<OrderPlaced>
{
    public async Task ConsumeAsync(IConsumerContext<OrderPlaced> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        logger.LogInformation("Consumer Pipeline behavior for Order Placed : {@Message}", context.Message);
        await Task.Delay(TimeSpan.FromSeconds(3), token);
        await next.Invoke(token);
    }
}
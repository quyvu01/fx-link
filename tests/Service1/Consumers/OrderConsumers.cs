using FxLink.Core.Abstractions;
using Service1.Dtos;

namespace Service1.Consumers;

public sealed class OrderConsumers(ILogger<OrderConsumers> logger) :
    IConsumer<OrderPlaced>,
    IConsumer<OrderCancelled>
{
    public Task ConsumeAsync(IConsumerContext<OrderPlaced> context, CancellationToken token = default)
    {
        logger.LogInformation("Order placed : {OrderId} - {Time}", context.Message.OrderId, context.Message.OrderTime);
        return Task.CompletedTask;
    }

    public Task ConsumeAsync(IConsumerContext<OrderCancelled> context, CancellationToken token = default)
    {
        logger.LogInformation("Order cancelled : {OrderId} - {Time}", context.Message.OrderId,
            context.Message.CancelledTime);
        return Task.CompletedTask;
    }
}
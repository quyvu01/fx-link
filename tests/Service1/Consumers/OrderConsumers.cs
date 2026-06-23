using FxLink.Abstractions;
using Service1.Dtos;

namespace Service1.Consumers;

public sealed class OrderConsumers(ILogger<OrderConsumers> logger) :
    IConsumer<OrderPlaced>,
    IConsumer<OrderCancelled>
{
    public Task ConsumeAsync(IConsumerContext<OrderPlaced> context, CancellationToken token = default)
    {
        logger.LogInformation("Order placed : {@Order}", context);
        return Task.CompletedTask;
    }

    public Task ConsumeAsync(IConsumerContext<OrderCancelled> context, CancellationToken token = default)
    {
        logger.LogInformation("Order cancelled : {@Order}", context.Message);
        return Task.CompletedTask;
    }
}
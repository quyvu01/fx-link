using FxLink.Abstractions;
using FxLink.Contexts;
using Order.Dtos.Orders;

namespace Order.Consumers;

// Plain pub/sub demo: no state machine involved, just IPublisher -> IConsumer.
public sealed class OrderConsumers(ILogger<OrderConsumers> logger) : IConsumer<OrderPlaced>
{
    public async Task ConsumeAsync(IConsumerContext<OrderPlaced> context, CancellationToken token = default)
    {
        logger.LogInformation("Order placed: {@Order}", context.Message);
        await Task.Delay(TimeSpan.FromSeconds(5), token);
    }
}
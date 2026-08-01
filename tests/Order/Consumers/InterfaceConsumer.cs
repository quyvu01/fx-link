using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Order.Dtos.Orders;

namespace Order.Consumers;

public sealed class InterfaceConsumer(ILogger<InterfaceConsumer> logger) : IConsumer<IOrderCreated>
{
    public async Task ConsumeAsync(IConsumerContext<IOrderCreated> context, CancellationToken token = default)
    {
        await Task.Delay(1000, token);
        logger.LogInformation("Order: {@Order}", context.Message);
        await context.ResponseAsync<IOrderResponse>(context.Message, token);
    }
}
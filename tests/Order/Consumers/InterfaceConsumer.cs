using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Extensions;
using Order.Dtos.Orders;

namespace Order.Consumers;

public sealed class InterfaceConsumer(ILogger<InterfaceConsumer> logger) : IConsumer<IOrderCreated>
{
    public async Task ConsumeAsync(IConsumerContext<IOrderCreated> context, CancellationToken token = default)
    {
        await Task.Delay(1000, token);
        logger.LogInformation("Order: {@Order}", context.Message);
        var result = await context
            .RequestAsync<IExtendedOrderRequest, IExtendedOrderResponse>(new { context.Message.OrderId }, token: token);
        logger.LogInformation("Response new: {@NewResponse}", result.Message);
        var newPrice = result.Message.Price;
        await context.ResponseAsync<IOrderResponse>(
            new { context.Message.OrderId, Price = context.Message.Price + newPrice }, token);
    }
}
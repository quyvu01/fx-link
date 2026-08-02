using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Order.Dtos.Orders;

namespace Order.Consumers;

public class ExtendedConsumer : IConsumer<IExtendedOrderRequest>
{
    public async Task ConsumeAsync(IConsumerContext<IExtendedOrderRequest> context, CancellationToken token = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await context.ResponseAsync<IExtendedOrderResponse>(new { context.Message.OrderId, Price = 10 }, token);
    }
}
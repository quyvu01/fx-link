using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Service1.Dtos.Orders;

namespace Service1.Consumers;

public sealed class OrderConsumers(ILogger<OrderConsumers> logger) :
    IConsumer<OrderCreated>,
    IConsumer<OrderPublisherTest>,
    IConsumer<OrderSucceed>,
    IConsumer<GetOrderHistory>
{
    public Task ConsumeAsync(IConsumerContext<OrderPublisherTest> context, CancellationToken token = default)
    {
        logger.LogInformation("Order publisher, just for test : {@Order}", context.Message);
        return Task.CompletedTask;
    }

    public Task ConsumeAsync(IConsumerContext<OrderSucceed> context, CancellationToken token = default)
    {
        logger.LogInformation("Order succeed, haha : {@Order}", context.Message);
        return Task.CompletedTask;
    }

    // A regular consumer can be registered for the same message type the state machine reacts to -
    // both fire independently.
    public Task ConsumeAsync(IConsumerContext<OrderCreated> context, CancellationToken token = default)
    {
        logger.LogInformation("OrderCreated observed outside the state machine: {@Order}", context.Message);
        return Task.CompletedTask;
    }

    public async Task ConsumeAsync(IConsumerContext<GetOrderHistory> context, CancellationToken token = default)
    {
        logger.LogInformation("[GetOrderHistory] : {@Order}", context.Message);
        await Task.Delay(TimeSpan.FromSeconds(2), token); // Simulating some actions!
        if (context.Message.ForceFail)
            throw new InvalidOperationException($"Simulated history lookup failure for order {context.Message.OrderId}");

        await context.ResponseAsync(
            new OrderHistoryResponse { OrderId = context.Message.OrderId, Historical = "Has some historical" }, token);
    }
}

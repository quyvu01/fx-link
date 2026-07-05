using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Service1.Dtos;
using Service1.StateMachines.Events;

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

    public Task ConsumeAsync(IConsumerContext<OrderCreated> context, CancellationToken token = default)
    {
        logger.LogInformation("OrderCreated, not state machine: {@Order}", context.Message);
        return Task.CompletedTask;
    }

    public async Task ConsumeAsync(IConsumerContext<GetOrderHistory> context, CancellationToken token = default)
    {
        logger.LogInformation("[GetOrderHistory] : {@Order}", context.Message);
        await Task.Delay(TimeSpan.FromSeconds(2), token); // Simulating some actions!
        await context.ResponseAsync(
            new OrderHistoryResponse { OrderId = context.Message.OrderId, Historical = "Has some historical" }, token);
    }
}
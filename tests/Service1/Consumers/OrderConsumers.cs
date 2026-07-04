using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Service1.Dtos;
using Service1.StateMachines.Events;

namespace Service1.Consumers;

public sealed class OrderConsumers(ILogger<OrderConsumers> logger) :
    IConsumer<OrderPlaced>,
    IConsumer<OrderPublisherTest>,
    IConsumer<OrderSucceed>
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

    public Task ConsumeAsync(IConsumerContext<OrderPlaced> context, CancellationToken token = default)
    {
        logger.LogInformation("Order placed : {@Order}", context.Message);
        return Task.CompletedTask;
    }
}
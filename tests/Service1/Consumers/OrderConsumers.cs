using FxLink.Abstractions;
using Service1.Dtos;
using Service1.StateMachines.Events;

namespace Service1.Consumers;

public sealed class OrderConsumers(ILogger<OrderConsumers> logger) :
    IConsumer<OrderCreated>,
    IConsumer<OrderPublisherTest>,
    IConsumer<OrderSucceed>
{
    public Task ConsumeAsync(IConsumerContext<OrderCreated> context, CancellationToken token = default)
    {
        logger.LogInformation("Order created for normal order : {@Order}", context.Message);
        return Task.CompletedTask;
    }

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
}
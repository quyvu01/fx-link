using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Service1.StateMachines.Events;

namespace Service1.Consumers;

public sealed class EdgeConsumers(ILogger<EdgeConsumers> logger) : IConsumer<OrderHistoryResponse>
{
    public Task ConsumeAsync(IConsumerContext<OrderHistoryResponse> context, CancellationToken token = default)
    {
        logger.LogInformation("Unexpected result from OrderHistoryResponse: {@Message}", context.Message);
        return Task.CompletedTask;
    }
}
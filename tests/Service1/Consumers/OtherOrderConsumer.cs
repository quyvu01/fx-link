using FxLink.Core.Abstractions;
using Service1.Dtos;

namespace Service1.Consumers;

public sealed class OtherOrderConsumer(ILogger<OtherOrderConsumer> logger) : IConsumer<OrderPlaced>
{
    public Task ConsumeAsync(IConsumerContext<OrderPlaced> context, CancellationToken token = default)
    {
        logger.LogInformation("Other order placed: {@Order} with CorrelationId: {@CorrelationId}", context.Message,
            context.CorrelationId);
        return Task.CompletedTask;
    }
}
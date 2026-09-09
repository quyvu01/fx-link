using FxLink.Abstractions;
using FxLink.Contexts;
using Order.Dtos;

namespace Order.Consumers;

public sealed class RawJsonConsumer(ILogger<RawJsonConsumer> logger) : IConsumer<IRawJsonEvent>
{
    public Task ConsumeAsync(IConsumeContext<IRawJsonEvent> context, CancellationToken token = default)
    {
        logger.LogInformation("Received message from {@Msg}", context.Message);
        return Task.CompletedTask;
    }
}
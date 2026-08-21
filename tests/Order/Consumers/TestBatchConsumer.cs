using FxLink.Abstractions;
using FxLink.Contexts;
using Order.Dtos.Batches;

namespace Order.Consumers;

public sealed class TestBatchConsumer(ILogger<TestBatchConsumer> logger) : IConsumer<IBatch<IInventoryCreated>>
{
    public async Task ConsumeAsync(IConsumeContext<IBatch<IInventoryCreated>> context,
        CancellationToken token = default)
    {
        await Task.Yield();
        foreach (var msg in context.Message)
        {
            logger.LogInformation("Message is: {@Msg}", msg.Message);
        }
    }
}
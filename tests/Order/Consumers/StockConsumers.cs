using FxLink.Abstractions;
using FxLink.Contexts;
using Order.Outboxes.Messages;

namespace Order.Consumers;

public sealed class StockConsumers(ILogger<StockConsumers> logger) : IConsumer<IStockCreated>
{
    public Task ConsumeAsync(IConsumeContext<IStockCreated> context, CancellationToken token = default)
    {
        logger.LogInformation("[IStockCreated] message: {@Message}", context.Message);
        return Task.CompletedTask;
    }
}
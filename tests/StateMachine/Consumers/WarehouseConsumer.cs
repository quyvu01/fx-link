using FxLink.Abstractions;
using FxLink.Contexts;
using StateMachine.Dtos.Inventory;

namespace StateMachine.Consumers;

// Stands in for a downstream warehouse service: answers the state machine's Request(CheckWarehouseStock).
public sealed class WarehouseConsumer(ILogger<WarehouseConsumer> logger) : IConsumer<CheckWarehouseStock>
{
    public async Task ConsumeAsync(IConsumerContext<CheckWarehouseStock> context, CancellationToken token = default)
    {
        logger.LogInformation("[CheckWarehouseStock] : {@Message}", context.Message);
        await Task.Delay(TimeSpan.FromMilliseconds(200), token);

        if (string.Equals(context.Message.Sku, "FAIL", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Simulated warehouse lookup failure for order {context.Message.OrderId}");

        var inStock = !string.Equals(context.Message.Sku, "OUT_OF_STOCK", StringComparison.OrdinalIgnoreCase);
        await context.ResponseAsync(new WarehouseStockResponse { OrderId = context.Message.OrderId, InStock = inStock },
            token);
    }
}
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Contexts;
using StateMachine.Dtos.Inventory;

namespace StateMachine.StateMachines.Inventory.Activities;

// Message-typed activity: bound via `.Activity(c => c.OfType<T>())`, so it has access to both the
// instance and the triggering message (AdjustStock here).
public sealed class StockAdjustmentActivity(ILogger<StockAdjustmentActivity> logger) :
    IStateMachineActivity<InventoryReservationInstance, AdjustStock>
{
    public Task ExecuteAsync(IStateMachineActivityContext<InventoryReservationInstance, AdjustStock> context,
        CancellationToken token = default)
    {
        context.Instance.Quantity = context.Message.NewQuantity;
        logger.LogInformation("[StockAdjustmentActivity] adjusted reservation {@OrderId} to quantity {@Quantity}",
            context.Message.OrderId, context.Message.NewQuantity);
        return Task.CompletedTask;
    }

    public Task FaultedAsync(IStateMachineActivityContext<InventoryReservationInstance, AdjustStock> context,
        Exception exception, CancellationToken token = default)
    {
        logger.LogWarning(exception, "[StockAdjustmentActivity] failed for reservation {@OrderId}",
            context.Message.OrderId);
        return Task.CompletedTask;
    }
}
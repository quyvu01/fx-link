using FxLink.StateMachine.Abstractions;

namespace StateMachine.StateMachines.Inventory.Activities;

public sealed class InventoryConfirmationActivity(ILogger<InventoryConfirmationActivity> logger) :
    IStateMachineActivity<InventoryReservationInstance>
{
    public Task ExecuteAsync(IStateMachineActivityContext<InventoryReservationInstance> context,
        CancellationToken token = default)
    {
        logger.LogInformation("[InventoryConfirmationActivity] confirming reservation: {@Instance}",
            context.Instance);
        context.TranslationTo(nameof(InventoryReservationStateMachine.Confirmed));
        return Task.CompletedTask;
    }

    public Task FaultedAsync(IStateMachineActivityContext<InventoryReservationInstance> context, Exception exception,
        CancellationToken token = default)
    {
        logger.LogWarning(exception, "[InventoryConfirmationActivity] failed for reservation: {@Instance}",
            context.Instance);
        return Task.CompletedTask;
    }
}

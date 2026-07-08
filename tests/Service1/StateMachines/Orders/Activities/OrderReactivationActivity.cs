using FxLink.StateMachine.Abstractions;
using Service1.Dtos.Orders;

namespace Service1.StateMachines.Orders.Activities;

// Message-typed activity: bound via `.Activity(c => c.OfType<T>())`, so it has access to both the
// instance and the triggering message (OrderReactivated here).
public sealed class OrderReactivationActivity(ILogger<OrderReactivationActivity> logger) :
    IStateMachineActivity<OrderStateMachineInstance, OrderReactivated>
{
    public Task ExecuteAsync(IStateMachineActivityContext<OrderStateMachineInstance, OrderReactivated> context,
        CancellationToken token = default)
    {
        logger.LogInformation("[OrderReactivationActivity] reactivating order {@OrderId}",
            context.Message.OrderId);
        context.TranslationTo(nameof(OrderStateMachine.OrderSucceed));
        return Task.CompletedTask;
    }

    public Task FaultedAsync(IStateMachineActivityContext<OrderStateMachineInstance, OrderReactivated> context,
        Exception exception, CancellationToken token = default)
    {
        logger.LogWarning(exception, "[OrderReactivationActivity] failed for order {@OrderId}",
            context.Message.OrderId);
        return Task.CompletedTask;
    }
}

using FxLink.StateMachine.Abstractions;

namespace Service1.StateMachines.Orders.Activities;

// Message-agnostic activity: bound via `.Activity(c => c.OfInstanceType<T>())`, so it only ever
// sees the instance, never the triggering message.
public sealed class OrderHistoryReviewActivity(ILogger<OrderHistoryReviewActivity> logger) :
    IStateMachineActivity<OrderStateMachineInstance>
{
    public Task ExecuteAsync(IStateMachineActivityContext<OrderStateMachineInstance> context,
        CancellationToken token = default)
    {
        logger.LogInformation("[OrderHistoryReviewActivity] reviewing history for instance: {@Instance}",
            context.Instance);
        context.TranslationTo(nameof(OrderStateMachine.OrderSucceed));
        return Task.CompletedTask;
    }

    public Task FaultedAsync(IStateMachineActivityContext<OrderStateMachineInstance> context, Exception exception,
        CancellationToken token = default)
    {
        logger.LogWarning(exception, "[OrderHistoryReviewActivity] failed for instance: {@Instance}",
            context.Instance);
        return Task.CompletedTask;
    }
}

using FxLink.StateMachine.Abstractions;
using Service1.StateMachines;

namespace Service1.Activities;

public sealed class OrderTestActivity(ILogger<OrderTestActivity> logger) :
    IStateMachineActivity<OrderStateMachineInstance>
{
    public Task ExecuteAsync(IStateMachineActivityContext<OrderStateMachineInstance> context,
        CancellationToken token = default)
    {
        logger.LogInformation("[OrderTestActivity] - instance only - activity: {@Context}", context);
        context.TranslationTo(nameof(OrderStateMachine.OrderSucceed));
        return Task.CompletedTask;
    }

    public Task FaultedAsync(IStateMachineActivityContext<OrderStateMachineInstance> context, Exception exception,
        CancellationToken token = default)
    {
        logger.LogInformation("[OrderTestActivity] - instance only failed: {@Context}", context);
        return Task.CompletedTask;
    }
}
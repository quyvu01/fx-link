using FxLink.StateMachine.Abstractions;
using Service1.StateMachines;
using Service1.StateMachines.Events;

namespace Service1.Activities;

public sealed class OrderTestActivity(ILogger<OrderTestActivity> logger)
    : IStateMachineActivity<OrderStateMachineInstance, OrderHistoryResponse>
{
    public Task ExecuteAsync(IStateMachineContext<OrderStateMachineInstance, OrderHistoryResponse> context,
        CancellationToken token = default)
    {
        logger.LogInformation("[OrderTestActivity] activity: {@Context}", context);
        return Task.CompletedTask;
    }

    public Task FaultedAsync(IStateMachineContext<OrderStateMachineInstance, OrderHistoryResponse> context, Exception e,
        CancellationToken token = default)
    {
        logger.LogInformation("[OrderTestActivity] failed: {@Context}", context);
        return Task.CompletedTask;
    }
}
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;

namespace FxLink.StateMachine.Implementations.Workflows;

public sealed class FlowOperator<TInstance, TMessage>(IEvent<TMessage> @event) : IFlowOperator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    public IEvent<TMessage> Event { get; } = @event;
    private readonly List<Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task>> _asyncActions = [];
    public Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task>[] AsyncActions => [.._asyncActions];

    public IFlowOperator<TInstance, TMessage> Then(Action<IStateMachineContext<TInstance, TMessage>> action)
    {
        _asyncActions.Add(ActionAsAsync);
        return this;

        Task ActionAsAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            action.Invoke(context);
            return Task.CompletedTask;
        }
    }

    public IFlowOperator<TInstance, TMessage> ThenAsync(
        Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task> asyncAction)
    {
        _asyncActions.Add(asyncAction);
        return this;
    }

    public IFlowOperator<TInstance, TMessage> TransitionTo(IState state)
    {
        Then(StateTransitionAction);
        return this;

        void StateTransitionAction(IStateMachineContext<TInstance, TMessage> context)
        {
            if (context.Instance is { } instance) instance.State = state.Name;
        }
    }

    public IFlowOperator<TInstance, TMessage> If(Func<IStateMachineContext<TInstance, TMessage>, bool> condition,
        Action<IFlowOperator<TInstance, TMessage>> activityCallback)
    {
        return IfAsync(ConditionAsync, activityCallback);

        Task<bool> ConditionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = condition.Invoke(context);
            return Task.FromResult(conditionResult);
        }
    }

    public IFlowOperator<TInstance, TMessage> IfAsync(
        Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task<bool>> condition,
        Action<IFlowOperator<TInstance, TMessage>> activityCallback)
    {
        ThenAsync(ConditionActionAsync);
        return this;

        async Task ConditionActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = await condition.Invoke(context, ct);
            if (!conditionResult) return;
            var newFlow = new FlowOperator<TInstance, TMessage>(Event);
            activityCallback.Invoke(newFlow);
            foreach (var asyncAction in newFlow.AsyncActions) await asyncAction.Invoke(context, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> IfElse(Func<IStateMachineContext<TInstance, TMessage>, bool> condition,
        Action<IFlowOperator<TInstance, TMessage>> activityCallback,
        Action<IFlowOperator<TInstance, TMessage>> otherwiseCallback)
    {
        return IfElseAsync(ConditionAsync, activityCallback, otherwiseCallback);

        Task<bool> ConditionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = condition.Invoke(context);
            return Task.FromResult(conditionResult);
        }
    }

    public IFlowOperator<TInstance, TMessage> IfElseAsync(
        Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task<bool>> condition,
        Action<IFlowOperator<TInstance, TMessage>> activityCallback,
        Action<IFlowOperator<TInstance, TMessage>> otherwiseCallback)
    {
        ThenAsync(ConditionActionAsync);
        return this;

        async Task ConditionActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = await condition.Invoke(context, ct);
            var newFlow = new FlowOperator<TInstance, TMessage>(Event);
            if (conditionResult) activityCallback.Invoke(newFlow);
            else otherwiseCallback.Invoke(newFlow);
            foreach (var asyncAction in newFlow.AsyncActions) await asyncAction.Invoke(context, ct);
        }
    }
}
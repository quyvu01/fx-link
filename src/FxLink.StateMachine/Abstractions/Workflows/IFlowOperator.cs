namespace FxLink.StateMachine.Abstractions.Workflows;

public interface IFlowOperator<TInstance, TMessage> : IFlow
    where TInstance : IStateMachineInstance where TMessage : class
{
    IEvent<TMessage> Event { get; }
    Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task>[] AsyncActions { get; }
    IFlowOperator<TInstance, TMessage> Then(Action<IStateMachineContext<TInstance, TMessage>> action);

    IFlowOperator<TInstance, TMessage> ThenAsync(
        Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task> asyncAction);

    IFlowOperator<TInstance, TMessage> TransitionTo(IState state);

    IFlowOperator<TInstance, TMessage> If(Func<IStateMachineContext<TInstance, TMessage>, bool> condition,
        Action<IFlowOperator<TInstance, TMessage>> activityCallback);

    IFlowOperator<TInstance, TMessage> IfAsync(
        Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task<bool>> condition,
        Action<IFlowOperator<TInstance, TMessage>> activityCallback);

    IFlowOperator<TInstance, TMessage> IfElse(
        Func<IStateMachineContext<TInstance, TMessage>, bool> condition,
        Action<IFlowOperator<TInstance, TMessage>> activityCallback,
        Action<IFlowOperator<TInstance, TMessage>> otherwiseCallback);

    IFlowOperator<TInstance, TMessage> IfElseAsync(
        Func<IStateMachineContext<TInstance, TMessage>, CancellationToken, Task<bool>> condition,
        Action<IFlowOperator<TInstance, TMessage>> activityCallback,
        Action<IFlowOperator<TInstance, TMessage>> otherwiseCallback);
}
using FxLink.StateMachine.Delegates;

namespace FxLink.StateMachine.Abstractions.Workflows;

public interface IFlowOperator<TInstance, TMessage> : IFlow
    where TInstance : IStateMachineInstance where TMessage : class
{
    IEvent<TMessage> Event { get; }
    AsyncOperatorAction<TInstance, TMessage>[] AsyncActions { get; }
    IFlowOperator<TInstance, TMessage> Then(OperatorAction<TInstance, TMessage> action);

    IFlowOperator<TInstance, TMessage> ThenAsync(AsyncOperatorAction<TInstance, TMessage> asyncAction);

    IFlowOperator<TInstance, TMessage> TransitionTo(IState state);

    IFlowOperator<TInstance, TMessage> If(OperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback);

    IFlowOperator<TInstance, TMessage> IfAsync(
        AsyncOperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback);

    IFlowOperator<TInstance, TMessage> IfElse(
        OperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback,
        ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback);

    IFlowOperator<TInstance, TMessage> IfElseAsync(
        AsyncOperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback,
        ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback);

    IFlowOperator<TInstance, TMessage> Publish<T>(MessageOperatorFactory<TInstance, TMessage, T> messageFactory)
        where T : class;

    IFlowOperator<TInstance, TMessage> PublishAsync<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class;

    IFlowOperator<TInstance, TMessage> Response<T>(MessageOperatorFactory<TInstance, TMessage, T> messageFactory)
        where T : class;

    IFlowOperator<TInstance, TMessage> ResponseAsync<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class;
}
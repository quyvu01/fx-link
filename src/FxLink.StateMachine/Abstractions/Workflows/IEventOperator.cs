using FxLink.StateMachine.Delegates;

namespace FxLink.StateMachine.Abstractions.Workflows;

public interface IEventOperator;

public interface IEventOperator<TInstance, TMessage> : IEventOperator
    where TInstance : IStateMachineInstance where TMessage : class
{
    IEvent<TMessage> Event { get; }
    Task ExecuteAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken token = default);
    IEventOperator<TInstance, TMessage> Then(OperatorAction<TInstance, TMessage> action);
    IEventOperator<TInstance, TMessage> ThenAsync(AsyncOperatorAction<TInstance, TMessage> asyncAction);

    IEventOperator<TInstance, TMessage> TransitionTo(IState state);
    IEventOperator<TInstance, TMessage> Complete();

    IEventOperator<TInstance, TMessage> If(OperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback);

    IEventOperator<TInstance, TMessage> IfAsync(
        AsyncOperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback);

    IEventOperator<TInstance, TMessage> IfElse(
        OperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> succeedCallback,
        ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback);

    IEventOperator<TInstance, TMessage> IfElseAsync(
        AsyncOperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> succeedCallback,
        ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback);

    IEventOperator<TInstance, TMessage> Publish<T>(MessageOperatorFactory<TInstance, TMessage, T> messageFactory)
        where T : class;

    IEventOperator<TInstance, TMessage> PublishAsync<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class;

    IEventOperator<TInstance, TMessage> Response<T>(MessageOperatorFactory<TInstance, TMessage, T> messageFactory)
        where T : class;

    IEventOperator<TInstance, TMessage> ResponseAsync<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class;

    IEventOperator<TInstance, TMessage> Schedule<T>(ISchedule<T> schedule,
        MessageOperatorFactory<TInstance, TMessage, T> messageFactory) where T : class;

    IEventOperator<TInstance, TMessage> ScheduleAsync<T>(ISchedule<T> schedule,
        MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class;

    IEventOperator<TInstance, TMessage> Unschedule<T>(ISchedule<T> schedule) where T : class;

    IEventOperator<TInstance, TMessage> Request<TRequest, TResponse>(IRequest<TRequest, TResponse> request,
        MessageOperatorFactory<TInstance, TMessage, TRequest> messageFactory)
        where TRequest : class where TResponse : class;

    IEventOperator<TInstance, TMessage> RequestAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request,
        MessageOperatorFactoryAsync<TInstance, TMessage, TRequest> messageFactoryAsync)
        where TRequest : class where TResponse : class;

    IEventOperator<TInstance, TMessage> Activity(StateMchineOperatorActivity<TInstance, TMessage> activity);
}

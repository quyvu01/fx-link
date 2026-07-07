namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineActivity;

public interface IStateMachineActivity<in TInstance, in TMessage> :
    IStateMachineActivity
    where TInstance : IStateMachineInstance where TMessage : class
{
    Task ExecuteAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken token = default);

    Task FaultedAsync(IStateMachineContext<TInstance, TMessage> context, Exception exception,
        CancellationToken token = default);
}
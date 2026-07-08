namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineActivity;

public interface IStateMachineActivity<in TInstance> :
    IStateMachineActivity where TInstance : IStateMachineInstance
{
    Task ExecuteAsync(IStateMachineActivityContext<TInstance> context, CancellationToken token = default);

    Task FaultedAsync(IStateMachineActivityContext<TInstance> context, Exception exception,
        CancellationToken token = default);
}

public interface IStateMachineActivity<in TInstance, in TMessage> :
    IStateMachineActivity
    where TInstance : IStateMachineInstance where TMessage : class
{
    Task ExecuteAsync(IStateMachineActivityContext<TInstance, TMessage> context, CancellationToken token = default);

    Task FaultedAsync(IStateMachineActivityContext<TInstance, TMessage> context, Exception exception,
        CancellationToken token = default);
}
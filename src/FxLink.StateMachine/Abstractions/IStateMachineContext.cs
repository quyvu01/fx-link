namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineContext<out TInstance> where TInstance : IStateMachineInstance
{
    TInstance Instance { get; }
    Guid CorrelationId { get; }
    Guid? RequesterId { get; }
    Dictionary<string, object> Headers { get; }
}

public interface IStateMachineContext<out TInstance, out TMessage> : IStateMachineContext<TInstance>
    where TInstance : IStateMachineInstance where TMessage : class
{
    TMessage Message { get; }
}
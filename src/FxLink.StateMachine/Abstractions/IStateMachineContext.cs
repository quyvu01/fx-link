namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineContext<out TInstance, out TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    TInstance Instance { get; }
    TMessage Message { get; }
    Guid CorrelationId { get; }
    Dictionary<string, object> Headers { get; }
}
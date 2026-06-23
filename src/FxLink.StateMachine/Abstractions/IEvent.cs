namespace FxLink.StateMachine.Abstractions;

public interface IEvent;
// Dispatch the message consumer
public interface IEvent<TMessage> : IEvent where TMessage : class
{
    
}
namespace FxLink.StateMachine.Abstractions;

public interface IEvent<TMessage> : IActivity where TMessage : class
{
    string Name { get; }
}
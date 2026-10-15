namespace FxLink.StateMachine.Abstractions;

public interface IEvent : IActivity;

public interface IEvent<TMessage> : IEvent where TMessage : class;
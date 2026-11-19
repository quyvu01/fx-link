namespace FxLink.Abstractions.StateMachine;

public interface IEvent : IActivity;

public interface IEvent<TMessage> : IEvent where TMessage : class;

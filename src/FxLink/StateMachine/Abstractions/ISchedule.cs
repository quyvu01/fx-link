namespace FxLink.StateMachine.Abstractions;

public interface ISchedule<TMessage> : IActivity where TMessage : class
{
    IEvent<TMessage> Received { get; }
}
namespace FxLink.Abstractions.StateMachine;

public interface ISchedule<TMessage> : IActivity where TMessage : class
{
    IEvent<TMessage> Received { get; }
}

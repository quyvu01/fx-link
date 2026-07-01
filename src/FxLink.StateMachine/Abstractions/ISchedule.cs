namespace FxLink.StateMachine.Abstractions;

public interface ISchedule<TMessage> : IActivity where TMessage : class
{
    string Name { get; }
    IEvent<TMessage> Received { get; }
}
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Implementations;

internal sealed record Schedule<TMessage> : ISchedule<TMessage> where TMessage : class
{
    public string Name { get; } = typeof(TMessage).Name;
    public IEvent<TMessage> Received { get; } = new Event<TMessage>();
}
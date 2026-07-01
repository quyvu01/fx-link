using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Implementations;

internal sealed record Event<TMessage> : IEvent<TMessage> where TMessage : class
{
    public string Name { get; } = typeof(TMessage).Name;
}
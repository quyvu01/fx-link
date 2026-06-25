using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Implementations;

internal abstract record Event
{
    internal abstract void SetName(string name);
}

internal sealed record Event<TMessage> : Event, IEvent<TMessage> where TMessage : class
{
    public string Name { get; private set; }
    internal override void SetName(string name) => Name = name;
    public bool Equals(Event<TMessage> other) => other != null && GetType() == other.GetType();
    public override int GetHashCode() => base.GetHashCode();
}
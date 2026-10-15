using FxLink.StateMachine.Abstractions;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace FxLink.StateMachine.Implementations;

internal sealed record Event<TMessage> : IEvent<TMessage> where TMessage : class
{
    public string Name { get; set; } = typeof(TMessage).Name;

    public bool Equals(Event<TMessage> other)
    {
        if (other is null) return false;
        // Default name => it means that it is the root level event.
        if (other.Name == typeof(TMessage).Name) return true;
        return Name == other.Name;
    }

    public override int GetHashCode() => Name.GetHashCode();
}
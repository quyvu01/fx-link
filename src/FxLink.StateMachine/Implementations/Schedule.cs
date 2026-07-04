using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Extensions;

namespace FxLink.StateMachine.Implementations;

internal sealed record Schedule<TMessage> : ISchedule<TMessage>, IInternalEventConfigurator where TMessage : class
{
    public string Name { get; set; } = typeof(TMessage).Name;
    public IEvent<TMessage> Received { get; } = new Event<TMessage>();

    public void Configure() => Received.SetName($"{Name}.{nameof(Received)}");
}
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Implementations;

internal sealed record Event<TMessage>(string Name) : IEvent<TMessage> where TMessage : class;
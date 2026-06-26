using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Contexts;

internal record StateMachineContext<TInstance, TMessage>(
    TInstance Instance,
    TMessage Message,
    Guid CorrelationId,
    Dictionary<string, object> Headers)
    : IStateMachineContext<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class;
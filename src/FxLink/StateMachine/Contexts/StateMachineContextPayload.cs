using System.Collections.Concurrent;
using FxLink.Contexts;
using FxLink.Exceptions;

namespace FxLink.StateMachine.Contexts;

internal abstract class StateMachineContextPayload : IContextPayload
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _contextPayloads = [];

    public T GetPayload<T>()
    {
        if (!_contextPayloads.TryGetValue(typeof(T), out var payload))
            throw new FxLinkException.ContextPayloadNotFound(typeof(T));
        return (T)payload.Value ?? throw new FxLinkException.ContextPayloadNotFound(typeof(T));
    }

    public void SetPayload<T>(T payload) => _contextPayloads[typeof(T)] = new Lazy<object>(() => payload);
}
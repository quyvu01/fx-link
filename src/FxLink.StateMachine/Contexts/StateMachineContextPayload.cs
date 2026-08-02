using System.Collections.Concurrent;
using FxLink.Abstractions.Contexts;

namespace FxLink.StateMachine.Contexts;

internal abstract class StateMachineContextPayload : IContextPayload
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _contextPayloads = [];

    public T GetPayload<T>()
    {
        var payload = _contextPayloads
            .GetValueOrDefault(typeof(T));
        if (!payload.IsValueCreated) throw new Exception();
        return (T)payload.Value ?? throw new Exception();
    }

    public void SetPayload<T>(T payload) => _contextPayloads[typeof(T)] = new Lazy<object>(() => payload);
}
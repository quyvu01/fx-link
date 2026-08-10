using System.Collections.Concurrent;
using FxLink.Abstractions;

namespace FxLink.Implementations;

internal sealed class MessageKeys : IMessageKeys
{
    private readonly ConcurrentDictionary<Type, HashSet<Type>> _messageKeys = [];

    public void AddMessageKey(Type messageType, Type messageKey)
    {
        var keys = _messageKeys.GetOrAdd(messageType, _ => []);
        keys.Add(messageKey);
    }

    public Type[] GetKeysByMessageType(Type messageType) => [.. _messageKeys.GetValueOrDefault(messageType, [])];

    public IReadOnlyDictionary<Type, Type[]> GetMessageKeys() =>
        _messageKeys.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
}
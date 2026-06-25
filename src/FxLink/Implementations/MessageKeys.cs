using System.Collections.Concurrent;
using FxLink.Abstractions;

namespace FxLink.Implementations;

internal sealed class MessageKeys : IMessageKeys
{
    private readonly ConcurrentDictionary<Type, HashSet<object>> _messageKeys = [];

    public void AddMessageKey(Type messageType, object messageKey)
    {
        var keys = _messageKeys.GetOrAdd(messageType, _ => []);
        keys.Add(messageKey);
    }

    public object[] GetMessageKeys(Type messageType) => _messageKeys.GetValueOrDefault(messageType, []).ToArray();
}
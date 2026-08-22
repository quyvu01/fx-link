using System.Collections;
using FxLink.Abstractions;
using FxLink.Contexts;

namespace FxLink.Implementations;

internal sealed class MessageBatch<TMessage> : IBatch<TMessage> where TMessage : class
{
    private readonly IReadOnlyList<IConsumeContext<TMessage>> _messages;

    public MessageBatch(IReadOnlyList<IConsumeContext<TMessage>> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        _messages = messages;
    }

    public IConsumeContext<TMessage> this[int index] => _messages[index];
    public int Length => _messages.Count;

    public IEnumerator<IConsumeContext<TMessage>> GetEnumerator() => _messages.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

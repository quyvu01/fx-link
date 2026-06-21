using FxLink.Core.Abstractions;

namespace FxLink.Core.Contracts;

public sealed class ConsumerContext<TMessage>(TMessage message, Guid correlationId, Dictionary<string, object> headers)
    : IConsumerContext<TMessage>
    where TMessage : class
{
    public Guid CorrelationId { get; } = correlationId;
    public Dictionary<string, object> Headers { get; } = headers;
    public TMessage Message { get; } = message;
}
using FxLink.Core.Abstractions;

namespace FxLink.Core.Contracts;

public sealed class PublisherContext(Guid correlationId, Dictionary<string, object> headers) : IPublisherContext
{
    public Guid CorrelationId { get; } = correlationId;
    public Dictionary<string, object> Headers { get; } = headers;
}
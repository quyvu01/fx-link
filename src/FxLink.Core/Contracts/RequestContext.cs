using FxLink.Core.Abstractions;

namespace FxLink.Core.Contracts;

public sealed class RequestContext(Guid correlationId, Dictionary<string, object> headers) : IRequestContext
{
    public Guid CorrelationId { get; } = correlationId;
    public Dictionary<string, object> Headers { get; } = headers;
}
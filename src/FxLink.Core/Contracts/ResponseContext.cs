using FxLink.Core.Abstractions;

namespace FxLink.Core.Contracts;

public sealed class ResponseContext(Guid correlationId, Dictionary<string, object> headers) : IResponseContext
{
    public Guid CorrelationId { get; } = correlationId;
    public Dictionary<string, object> Headers { get; } = headers;
}
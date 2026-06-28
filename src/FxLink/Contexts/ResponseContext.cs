using FxLink.Abstractions;

namespace FxLink.Contexts;

public sealed class ResponseContext(Guid correlationId, Guid requesterId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IResponseContext
{
    public Guid RequesterId { get; } = requesterId;
}
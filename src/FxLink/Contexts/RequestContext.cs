using FxLink.Abstractions;

namespace FxLink.Contexts;

public sealed class RequestContext(Guid correlationId, Guid requesterId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IRequestContext
{
    public Guid RequesterId { get; } = requesterId;
}
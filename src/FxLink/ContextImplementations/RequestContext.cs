using FxLink.Abstractions;

namespace FxLink.ContextImplementations;

public sealed class RequestContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IRequestContext;
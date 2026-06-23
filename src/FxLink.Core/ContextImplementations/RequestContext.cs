using FxLink.Core.Abstractions;

namespace FxLink.Core.ContextImplementations;

public sealed class RequestContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IRequestContext;
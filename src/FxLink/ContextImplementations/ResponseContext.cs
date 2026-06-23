using FxLink.Abstractions;

namespace FxLink.ContextImplementations;

public sealed class ResponseContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IResponseContext;
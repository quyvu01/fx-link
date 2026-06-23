using FxLink.Core.Abstractions;

namespace FxLink.Core.ContextImplementations;

public sealed class ResponseContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IResponseContext;
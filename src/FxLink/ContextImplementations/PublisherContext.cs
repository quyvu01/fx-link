using FxLink.Abstractions;

namespace FxLink.ContextImplementations;

public sealed class PublisherContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IPublisherContext;
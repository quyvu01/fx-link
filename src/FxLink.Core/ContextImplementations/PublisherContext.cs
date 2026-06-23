using FxLink.Core.Abstractions;

namespace FxLink.Core.ContextImplementations;

public sealed class PublisherContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IPublisherContext;
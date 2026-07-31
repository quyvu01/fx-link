using FxLink.Statics;

namespace FxLink.Abstractions.Contexts;

public sealed class PublisherContext : AbstractContext, IPublisherContext
{
    internal PublisherContext(Guid correlationId, IHeaders headers)
        : base(correlationId, headers)
    {
    }

    public PublisherContext(IContext context)
        : this(context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    internal static PublisherContext New(IDictionary<string, object> headers = null) =>
        new(Id.New(), new HeaderBag(headers ?? new Dictionary<string, object>()));
}
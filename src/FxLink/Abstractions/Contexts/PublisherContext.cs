using FxLink.Statics;

namespace FxLink.Abstractions.Contexts;

public sealed class PublisherContext : AbstractContext, IPublisherContext
{
    internal PublisherContext(Guid correlationId, Dictionary<string, object> headers)
        : base(correlationId, headers)
    {
    }

    public PublisherContext(IContext context)
        : this(context.CorrelationId, new Dictionary<string, object>(context.Headers))
    {
    }

    public static PublisherContext New(IDictionary<string, object> headers = null) =>
        new(Id.New(), new Dictionary<string, object>(headers ?? new Dictionary<string, object>()));
}
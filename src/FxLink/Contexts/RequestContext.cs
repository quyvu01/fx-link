using FxLink.Statics;

namespace FxLink.Contexts;

public sealed class RequestContext : AbstractContext, IRequestContext
{
    internal RequestContext(IHeaders headers, Guid correlationId)
        : base(headers, correlationId)
    {
    }

    public RequestContext(IContext context)
        : this(new HeaderBag(context.Headers), context.CorrelationId)
    {
    }

    public Guid RequesterId { get; } = Id.New();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan? TimeToLive { get; set; } = TimeSpan.FromSeconds(30);

    public static RequestContext New(TimeSpan? timeout = null, TimeSpan? timeToLive = null,
        IDictionary<string, object> headers = null)
    {
        var headerCloned = new HeaderBag(headers ?? new Dictionary<string, object>());
        var context = new RequestContext(headerCloned, Id.New());
        if (timeout is { } t) context.Timeout = t;
        if (timeToLive is { } ttl) context.TimeToLive = ttl;
        return context;
    }
}

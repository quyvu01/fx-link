using FxLink.Statics;

namespace FxLink.Abstractions.Contexts;

public sealed class RequestContext : AbstractContext, IRequestContext
{
    internal RequestContext(Guid correlationId, IHeaders headers)
        : base(correlationId, headers)
    {
    }

    public RequestContext(IContext context)
        : this(context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    public Guid RequesterId { get; internal set; } = Id.New();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan? TimeToLive { get; set; } = TimeSpan.FromSeconds(30);

    public static RequestContext New(TimeSpan? timeout = null, TimeSpan? timeToLive = null,
        IDictionary<string, object> headers = null)
    {
        var headerCloned = new HeaderBag(headers ?? new Dictionary<string, object>());
        var context = new RequestContext(Id.New(), headerCloned);
        if (timeout is { } t) context.Timeout = t;
        if (timeToLive is { } ttl) context.TimeToLive = ttl;
        return context;
    }
}

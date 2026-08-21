using FxLink.Statics;

namespace FxLink.Contexts;

public class PublisherContext : AbstractContext, IPublisherContext
{
    internal PublisherContext(IHeaders headers, Guid correlationId)
        : base(headers, correlationId)
    {
    }

    public PublisherContext(IContext context)
        : this(new HeaderBag(context.Headers), context.CorrelationId)
    {
        if (context is not IPublisherContext p) return;
        DelayTime = p.DelayTime;
        TimeToLive = p.TimeToLive;
        ScheduleToken = p.ScheduleToken;
        RequesterId = p.RequesterId;
    }

    public TimeSpan? DelayTime { get; set; }
    public TimeSpan? TimeToLive { get; set; }
    public Guid? ScheduleToken { get; set; }
    public Guid? RequesterId { get; set; }

    internal static PublisherContext New(IDictionary<string, object> headers = null) =>
        new(new HeaderBag(headers ?? new Dictionary<string, object>()), Id.New());
}
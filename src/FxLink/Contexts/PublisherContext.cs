using FxLink.Statics;

namespace FxLink.Contexts;

public sealed class PublisherContext : AbstractContext, IPublisherContext
{
    internal PublisherContext(Guid correlationId, IHeaders headers)
        : base(correlationId, headers)
    {
    }

    public PublisherContext(IContext context)
        : this(context.CorrelationId, new HeaderBag(context.Headers))
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
        new(Id.New(), new HeaderBag(headers ?? new Dictionary<string, object>()));
}
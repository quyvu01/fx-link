using FxLink.Statics;

namespace FxLink.Contexts;

public class PublishContext : AbstractContext, IPublishContext
{
    internal PublishContext(IHeaders headers, Guid correlationId)
        : base(headers, correlationId)
    {
    }

    public PublishContext(IContext context)
        : this(new HeaderBag(context.Headers), context.CorrelationId)
    {
        if (context is not IPublishContext p) return;
        DelayTime = p.DelayTime;
        TimeToLive = p.TimeToLive;
        ScheduleToken = p.ScheduleToken;
        RequesterId = p.RequesterId;
    }

    public TimeSpan? DelayTime { get; set; }
    public TimeSpan? TimeToLive { get; set; }
    public Guid? ScheduleToken { get; set; }
    public Guid? RequesterId { get; set; }

    internal static PublishContext New(IDictionary<string, object> headers = null) =>
        new(new HeaderBag(headers ?? new Dictionary<string, object>()), Id.New());
}
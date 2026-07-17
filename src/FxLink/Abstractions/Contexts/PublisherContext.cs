namespace FxLink.Abstractions.Contexts;

public sealed class PublisherContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IPublisherContext
{
    public PublisherContext(IContext context) : this(context.CorrelationId, context.Headers)
    {
    }
    //
    // public TimeSpan? Delay { get; set; }
    // public Guid? ScheduledMessageId { get; set; }
}
using FxLink.Abstractions;

namespace FxLink.Contexts;

public sealed class PublisherContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IPublisherContext
{
    public TimeSpan? Delay { get; set; }
    public Guid? ScheduledMessageId { get; set; }
}
namespace FxLink.Abstractions.Contexts;

public sealed class RequestContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IRequestContext
{
    public Guid RequesterId { get; internal set; } = Guid.NewGuid();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan? TimeToLive { get; set; } = TimeSpan.FromSeconds(30);

    public RequestContext(IContext context) : this(context.CorrelationId, context.Headers)
    {
    }
}
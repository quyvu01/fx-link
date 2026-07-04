namespace FxLink.Abstractions.Contexts;

public sealed class RequestContext(Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IRequestContext
{
    public Guid RequesterId { get; } = Guid.NewGuid();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan? TimeToLive { get; set; } = TimeSpan.FromSeconds(60);

    public RequestContext(IContext context) : this(context.CorrelationId, context.Headers)
    {
    }
}
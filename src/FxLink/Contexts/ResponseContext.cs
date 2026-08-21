namespace FxLink.Contexts;

public class ResponseContext : AbstractContext, IResponseContext
{
    internal ResponseContext(IHeaders headers, Guid correlationId, Guid requesterId, TimeSpan? timeToLive = null)
        : base(headers, correlationId)
    {
        RequesterId = requesterId;
        TimeToLive = timeToLive;
    }

    // The response leg of request/reply had no TTL of its own — a response could sit in the reply
    // queue indefinitely even after the requester already gave up waiting. Carry forward the
    // originating request's TimeToLive (if the source context is a consumed request, i.e.
    // IConsumerContext) so RabbitMqClientConnector can set the same wire-level expiration on the way
    // back.
    public ResponseContext(IContext context, Guid requesterId)
        : this(new HeaderBag(context.Headers), context.CorrelationId, requesterId,
            (context as IConsumeContext)?.TimeToLive)
    {
    }

    public Guid RequesterId { get; }
    public TimeSpan? TimeToLive { get; }
}

internal sealed class ResponseContext<TResponse> : ResponseContext, IResponseContext<TResponse>
    where TResponse : class
{
    internal ResponseContext(TResponse message, IHeaders headers, Guid correlationId, Guid requesterId,
        TimeSpan? timeToLive = null)
        : base(headers, correlationId, requesterId, timeToLive) => Message = message;

    public ResponseContext(TResponse message, IContext context, Guid requesterId)
        : this(message, new HeaderBag(context.Headers), context.CorrelationId, requesterId,
            (context as IConsumeContext)?.TimeToLive)
    {
    }

    public TResponse Message { get; }
}

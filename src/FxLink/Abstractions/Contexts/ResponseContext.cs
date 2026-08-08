namespace FxLink.Abstractions.Contexts;

public class ResponseContext : AbstractContext, IResponseContext
{
    internal ResponseContext(Guid requesterId, Guid correlationId, IHeaders headers, TimeSpan? timeToLive = null)
        : base(correlationId, headers)
    {
        RequesterId = requesterId;
        TimeToLive = timeToLive;
    }

    // The response leg of request/reply had no TTL of its own — a response could sit in the reply
    // queue indefinitely even after the requester already gave up waiting. Carry forward the
    // originating request's TimeToLive (if the source context is a consumed request, i.e.
    // IConsumerContext) so RabbitMqClientConnector can set the same wire-level expiration on the way
    // back.
    public ResponseContext(Guid requesterId, IContext context)
        : this(requesterId, context.CorrelationId, new HeaderBag(context.Headers),
            (context as IConsumerContext)?.TimeToLive)
    {
    }

    public Guid RequesterId { get; }
    public TimeSpan? TimeToLive { get; }
}

internal sealed class ResponseContext<TResponse> : ResponseContext, IResponseContext<TResponse>
    where TResponse : class
{
    internal ResponseContext(TResponse message, Guid requesterId, Guid correlationId, IHeaders headers,
        TimeSpan? timeToLive = null)
        : base(requesterId, correlationId, headers, timeToLive) => Message = message;

    public ResponseContext(TResponse message, Guid requesterId, IContext context)
        : this(message, requesterId, context.CorrelationId, new HeaderBag(context.Headers),
            (context as IConsumerContext)?.TimeToLive)
    {
    }

    public TResponse Message { get; }
}

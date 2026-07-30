namespace FxLink.Abstractions.Contexts;

public class ResponseContext : AbstractContext, IResponseContext
{
    internal ResponseContext(Guid requesterId, Guid correlationId, IHeaders headers)
        : base(correlationId, headers) => RequesterId = requesterId;

    public ResponseContext(Guid requesterId, IContext context)
        : this(requesterId, context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    public Guid RequesterId { get; }
}

internal sealed class ResponseContext<TResponse> : ResponseContext, IResponseContext<TResponse>
    where TResponse : class
{
    internal ResponseContext(TResponse message, Guid requesterId, Guid correlationId,
        IHeaders headers)
        : base(requesterId, correlationId, headers) => Message = message;

    public ResponseContext(TResponse message, Guid requesterId, IContext context)
        : this(message, requesterId, context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    public TResponse Message { get; }
}

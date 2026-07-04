namespace FxLink.Abstractions.Contexts;

public class ResponseContext(
    Guid requesterId,
    Guid correlationId,
    Dictionary<string, object> headers) : AbstractContext(correlationId, headers), IResponseContext
{
    public ResponseContext(Guid requesterId, IContext context) : this(requesterId, context.CorrelationId,
        context.Headers)
    {
    }

    public Guid RequesterId { get; } = requesterId;
}

internal sealed class ResponseContext<TResponse>(
    TResponse message,
    Guid requesterId,
    Guid correlationId,
    Dictionary<string, object> headers)
    : ResponseContext(requesterId, correlationId, headers), IResponseContext<TResponse> where TResponse : class
{
    public ResponseContext(TResponse message, Guid requesterId, IContext context) : this(message, requesterId,
        context.CorrelationId, context.Headers)
    {
    }

    public TResponse Message { get; } = message;
}
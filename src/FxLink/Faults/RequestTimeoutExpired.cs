namespace FxLink.Faults;

public class RequestTimeoutExpired<TRequest>(
    TRequest message,
    Guid correlationId,
    DateTime expirationTime,
    Guid requestId)
    where TRequest : class
{
    public Guid CorrelationId { get; } = correlationId;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public DateTime ExpirationTime { get; } = expirationTime;
    public Guid RequestId { get; } = requestId;
    public TRequest Message { get; } = message;
}
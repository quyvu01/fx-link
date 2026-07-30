namespace FxLink.Abstractions.Contexts;

public abstract class AbstractContext(Guid correlationId, IHeaders headers) : IContext
{
    public Guid CorrelationId { get; } = correlationId;
    public IHeaders Headers { get; } = headers;
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo { get; } = Contexts.HostInfo.Current;
}
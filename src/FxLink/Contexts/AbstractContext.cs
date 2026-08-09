namespace FxLink.Contexts;

public abstract class AbstractContext(Guid correlationId, IHeaders headers, DateTime? sentTime = null,
    IHostInfo hostInfo = null) : IContext
{
    public Guid CorrelationId { get; } = correlationId;
    public IHeaders Headers { get; } = headers;
    public DateTime? SentTime { get; } = sentTime ?? DateTime.UtcNow;
    public IHostInfo HostInfo { get; } = hostInfo ?? Contexts.HostInfo.Current;
}

namespace FxLink.Abstractions.Contexts;

public abstract class AbstractContext(Guid correlationId, Dictionary<string, object> headers) : IContext
{
    public Guid CorrelationId { get; } = correlationId;
    public Dictionary<string, object> Headers { get; } = headers;
    public DateTime? SentTime { get; internal set; } = DateTime.UtcNow;
    public IHostInfo HostInfo { get; internal set; } = Contexts.HostInfo.Current;
}
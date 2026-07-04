namespace FxLink.Abstractions.Contexts;

public abstract class AbstractContext(Guid correlationId, Dictionary<string, object> headers) : IContext
{
    public Guid CorrelationId { get; } = correlationId;
    public Dictionary<string, object> Headers { get; } = headers;
    public string MessageKey { get; set; }
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => Contexts.HostInfo.Current;
}
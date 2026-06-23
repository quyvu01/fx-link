using FxLink.Abstractions;

namespace FxLink.Contexts;

public abstract class AbstractContext(Guid correlationId, Dictionary<string, object> headers) : IContext
{
    public Guid CorrelationId { get; } = correlationId;
    public Dictionary<string, object> Headers { get; } = headers;
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => Abstractions.HostInfo.Current;
}
using FxLink.Statics;

namespace FxLink.Contexts;

public abstract class AbstractContext(IHeaders headers, Guid correlationId, DateTime? sentTime = null,
    IHostInfo hostInfo = null, Guid? messageId = null) : IContext
{
    public Guid MessageId { get; } = messageId ?? Id.New();
    public Guid CorrelationId { get; } = correlationId;
    public IHeaders Headers { get; } = headers;
    public DateTime? SentTime { get; } = sentTime ?? DateTime.UtcNow;
    public IHostInfo HostInfo { get; } = hostInfo ?? Contexts.HostInfo.Current;
}

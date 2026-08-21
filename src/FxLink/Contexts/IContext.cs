namespace FxLink.Contexts;

public interface IContext
{
    Guid MessageId { get; }
    Guid CorrelationId { get; }
    IHeaders Headers { get; }
    DateTime? SentTime { get; }
    IHostInfo HostInfo { get; }
}
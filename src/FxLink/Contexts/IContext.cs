namespace FxLink.Contexts;

public interface IContext
{
    Guid CorrelationId { get; }
    IHeaders Headers { get; }
    DateTime? SentTime { get; }
    IHostInfo HostInfo { get; }
}
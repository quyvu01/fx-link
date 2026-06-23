namespace FxLink.Abstractions;

public interface IContext
{
    Guid CorrelationId { get; }
    Dictionary<string, object> Headers { get; }
    DateTime? SentTime { get; }
    IHostInfo HostInfo { get; }
}
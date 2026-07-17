namespace FxLink.Abstractions.Contexts;

public interface IContext
{
    Guid CorrelationId { get; }
    Dictionary<string, object> Headers { get; }
    // string MessageKey { get; }
    DateTime? SentTime { get; }
    IHostInfo HostInfo { get; }
}
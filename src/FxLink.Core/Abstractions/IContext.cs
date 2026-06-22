namespace FxLink.Core.Abstractions;

public interface IContext
{
    Guid CorrelationId { get; }
    Dictionary<string, object> Headers { get; }
}
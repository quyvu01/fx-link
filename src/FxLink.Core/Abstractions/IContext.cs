namespace FxLink.Core.Abstractions;

public interface IContext
{
    Guid CorrelationId { get; }
    Dictionary<string, object> Headers { get; }
}

public interface IConsumerContext<out TMessage> : IContext where TMessage : class
{
    TMessage Message { get; }
}

public interface IPublisherContext : IContext;
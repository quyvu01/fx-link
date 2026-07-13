namespace FxLink.Abstractions.Contexts;

public interface IConsumerContext : IContext, IResponse, IPublisher;

public interface IConsumerContext<out TMessage> : IConsumerContext where TMessage : class
{
    Guid? RequesterId { get; }
    TMessage Message { get; }
    string RoutingKey { get; }
    int RetryCount { get; }
    void Retried();
}
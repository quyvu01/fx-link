namespace FxLink.Abstractions;

public interface IConsumerContext : IContext, IResponse, IPublisher;

public interface IConsumerContext<out TMessage> : IConsumerContext where TMessage : class
{
    Guid? RequesterId { get; }
    TMessage Message { get; }
}
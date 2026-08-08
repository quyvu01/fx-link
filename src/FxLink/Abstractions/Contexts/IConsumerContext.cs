namespace FxLink.Abstractions.Contexts;

public interface IConsumerContext : IContext, IResponse, IPublisher, IContextPayload
{
    TimeSpan? TimeToLive { get; }
}

public interface IConsumerContext<out TMessage> : IConsumerContext where TMessage : class
{
    Guid? RequesterId { get; }
    TMessage Message { get; }
}
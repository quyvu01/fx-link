using FxLink.Abstractions;

namespace FxLink.Contexts;

public interface IConsumeContext : IContext, IResponse, IPublisher, IContextPayload
{
    TimeSpan? TimeToLive { get; }
}

public interface IConsumeContext<out TMessage> : IConsumeContext where TMessage : class
{
    Guid? RequesterId { get; }
    TMessage Message { get; }
}
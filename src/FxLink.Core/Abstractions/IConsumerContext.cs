namespace FxLink.Core.Abstractions;

public interface IConsumerContext : IContext, IResponse;

public interface IConsumerContext<out TMessage> : IConsumerContext where TMessage : class
{
    TMessage Message { get; }
}
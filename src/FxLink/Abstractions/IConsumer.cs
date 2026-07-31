using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IConsumer : IAction;

public interface IConsumer<in TMessage> : IConsumer where TMessage : class
{
    Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default);
}
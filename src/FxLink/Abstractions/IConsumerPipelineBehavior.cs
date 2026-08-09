using FxLink.Contexts;
using FxLink.Delegates;

namespace FxLink.Abstractions;

public interface IConsumerPipelineBehavior;

public interface IConsumerPipelineBehavior<in TMessage> : IConsumerPipelineBehavior where TMessage : class
{
    Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default);
}
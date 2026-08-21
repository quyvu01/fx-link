using FxLink.Contexts;
using FxLink.Delegates;

namespace FxLink.Abstractions;

public interface IConsumerPipelineBehavior;

public interface IConsumerPipelineBehavior<in TMessage> : IConsumerPipelineBehavior where TMessage : class
{
    Task ConsumeAsync(IConsumeContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default);
}
using FxLink.Core.Abstractions;

namespace FxLink.Core.PipelineBehaviors;

internal class ConsumerPipelineBehaviorOrchestrator<TMessage>(
    IEnumerable<IConsumerPipelineBehavior<TMessage>> pipelineBehaviors,
    IServer<TMessage> server) where TMessage : class
{
    internal async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        var func = pipelineBehaviors
            .Reverse()
            .Aggregate(() => server.ConsumeAsync(context, token),
                (acc, next) => () => next.ConsumeAsync(context, acc, token));
        await func.Invoke();
    }
}
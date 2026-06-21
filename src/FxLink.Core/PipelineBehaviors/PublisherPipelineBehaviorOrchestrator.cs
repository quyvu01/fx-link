using FxLink.Core.Abstractions;

namespace FxLink.Core.PipelineBehaviors;

internal sealed class PublisherPipelineBehaviorOrchestrator<TMessage>(
    IEnumerable<IPublisherPipelineBehavior<TMessage>> pipelineBehaviors,
    IClient<TMessage> client) where TMessage : class
{
    public async Task PublishAsync(TMessage message, IPublisherContext context, CancellationToken token = default)
    {
        var func = pipelineBehaviors.Reverse()
            .Aggregate(() => client.SendAsync(message, context, token),
                (acc, next) => () => next.PublishAsync(message, context, acc, token));
        await func.Invoke();
    }
}
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.PipelineBehaviors;

namespace FxLink.Implementations;

internal class ServerConnector<TMessage>(ConsumerPipelineBehaviorOrchestrator<TMessage> pipelineBehavior)
    : IServerConnector<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        await pipelineBehavior.ExecuteAsync(context, token);
    }
}
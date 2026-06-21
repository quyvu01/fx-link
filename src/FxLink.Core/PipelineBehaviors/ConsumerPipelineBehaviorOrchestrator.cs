using FxLink.Core.Abstractions;
using FxLink.Core.Delegates;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.PipelineBehaviors;

internal class ConsumerPipelineBehaviorOrchestrator<TMessage>(IServiceProvider serviceProvider) where TMessage : class
{
    // Just lazy load pipeline behaviors and server because sometimes we want to defer services loaded on pipelines and server
    internal async Task ExecuteAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        var server = serviceProvider.GetRequiredService<IServer<TMessage>>();
        var pipelineBehaviors = serviceProvider
            .GetServices<IConsumerPipelineBehavior<TMessage>>();
        var func = pipelineBehaviors
            .Reverse()
            .Aggregate((ConsumerHandlerDelegate)(ct => server.ConsumeAsync(context, ct)),
                (acc, next) => ct => next.ConsumeAsync(context, acc, ct));
        await func.Invoke(token);
    }
}
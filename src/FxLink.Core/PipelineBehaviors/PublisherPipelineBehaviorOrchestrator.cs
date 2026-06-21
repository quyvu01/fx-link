using FxLink.Core.Abstractions;
using FxLink.Core.Delegates;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.PipelineBehaviors;

internal sealed class PublisherPipelineBehaviorOrchestrator<TMessage>(IServiceProvider serviceProvider)
    where TMessage : class
{
    // Just lazy load pipeline behaviors and client because sometimes we want to defer services loaded on pipelines and client
    public async Task ExecuteAsync(TMessage message, IPublisherContext context, CancellationToken token = default)
    {
        var client = serviceProvider.GetRequiredService<IClient<TMessage>>();
        var pipelineBehaviors = serviceProvider
            .GetServices<IPublisherPipelineBehavior<TMessage>>();
        var func = pipelineBehaviors.Reverse()
            .Aggregate((PublisherHandlerDelegate)(ct => client.SendAsync(message, context, ct)),
                (acc, next) => ct => next.PublishAsync(message, context, acc, ct));
        await func.Invoke(token);
    }
}
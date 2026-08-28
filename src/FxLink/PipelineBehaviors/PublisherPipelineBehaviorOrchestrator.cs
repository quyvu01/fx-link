using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.PipelineBehaviors;

internal sealed class PublisherPipelineBehaviorOrchestrator<TMessage>(IServiceProvider serviceProvider)
    where TMessage : class
{
    // Just lazy load pipeline behaviors and client because sometimes we want to defer services loaded on pipelines and client
    internal Task ExecuteAsync(TMessage message, IPublishContext context, CancellationToken token = default) =>
        ExecuteAsync(message, context, exclude: null, token);

    // exclude lets a caller that's already effectively "past" a given behavior — e.g. the Outbox
    // dispatcher re-publishing a row it already enqueued through OutboxPublisherPipelineBehavior —
    // skip just that one behavior while still running everything else (error logging, tracing, or
    // any other custom behavior a consumer of FxLink registers), instead of bypassing the pipeline
    // entirely and silently losing those cross-cutting concerns for dispatched messages.
    internal async Task ExecuteAsync(TMessage message, IPublishContext context,
        Func<IPublisherPipelineBehavior<TMessage>, bool> exclude, CancellationToken token = default)
    {
        var client = serviceProvider.GetRequiredService<IClientConnector<TMessage>>();
        var pipelineBehaviors = serviceProvider
            .GetServices<IPublisherPipelineBehavior<TMessage>>();
        if (exclude is not null) pipelineBehaviors = pipelineBehaviors.Where(b => !exclude(b));
        var func = pipelineBehaviors.Reverse()
            .Aggregate((PublisherHandlerDelegate)(ct => client.SendAsync(message, context, ct)),
                (acc, next) => ct => next.PublishAsync(message, context, acc, ct));
        await func.Invoke(token);
    }
}
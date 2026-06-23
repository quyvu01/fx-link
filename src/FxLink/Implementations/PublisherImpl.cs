using FxLink.Abstractions;
using FxLink.ContextImplementations;
using FxLink.PipelineBehaviors;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal sealed class PublisherImpl(IServiceProvider serviceProvider) : IPublisher
{
    public async Task PublishAsync<TMessage>(TMessage message, IPublisherContext context,
        CancellationToken token = default)
        where TMessage : class
    {
        var publisherOrchestrator = serviceProvider
            .GetRequiredService<PublisherPipelineBehaviorOrchestrator<TMessage>>();
        await publisherOrchestrator.ExecuteAsync(message, context, token);
    }

    public Task PublishAsync<TMessage>(TMessage message, CancellationToken token = default) where TMessage : class =>
        PublishAsync(message, new PublisherContext(Guid.NewGuid(), []), token);
}
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.PipelineBehaviors;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal sealed class Publisher(IServiceProvider serviceProvider) : IPublisher, IInternalContext
{
    public async Task PublishAsync<TMessage>(TMessage message, Action<IPublishContext> contextOptions = null,
        CancellationToken token = default) where TMessage : class
    {
        var context = Context switch
        {
            null => PublishContext.New(),
            IPublishContext publisherContext => publisherContext,
            { } ctx => new PublishContext(ctx)
        };
        contextOptions?.Invoke(context);
        var publisherOrchestrator = serviceProvider
            .GetRequiredService<PublisherPipelineBehaviorOrchestrator<TMessage>>();
        await publisherOrchestrator.ExecuteAsync(message, context, token);
    }

    public Task PublishAsync<TMessage>(TMessage message, CancellationToken token = default) where TMessage : class =>
        PublishAsync(message, null, token);

    public IContext Context { get; private set; }

    public void SetContext(IContext context) => Context = context;
}
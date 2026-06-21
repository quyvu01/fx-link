using FxLink.Core.Delegates;

namespace FxLink.Core.Abstractions;

public interface IPublisherPipelineBehavior;

public interface IPublisherPipelineBehavior<in TMessage> : IPublisherPipelineBehavior where TMessage : class
{
    Task PublishAsync(TMessage message, IPublisherContext context, PublisherHandlerDelegate next,
        CancellationToken token = default);
}
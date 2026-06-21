using FxLink.Core.Abstractions;
using FxLink.Core.Delegates;

namespace Service1.PipelineBehaviors;

public sealed class PublishPipelineBehavior<T>(ILogger<PublishPipelineBehavior<T>> logger)
    : IPublisherPipelineBehavior<T> where T : class
{
    public async Task PublishAsync(T message, IPublisherContext context, PublisherHandlerDelegate next,
        CancellationToken token = default)
    {
        logger.LogInformation("Publish pipeline behavior message: {@MessageType}, {@Message}", typeof(T).Name, message);
        await next.Invoke(token);
    }
}
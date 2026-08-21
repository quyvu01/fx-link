using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;
using Microsoft.Extensions.Logging;

namespace FxLink.InternalPipelineBehaviors;

internal sealed class PublisherErrorPipelineBehavior<TMessage>(ILogger<PublisherErrorPipelineBehavior<TMessage>> logger)
    : IPublisherPipelineBehavior<TMessage>
    where TMessage : class
{
    public async Task PublishAsync(TMessage message, IPublishContext context, PublisherHandlerDelegate next,
        CancellationToken token = default)
    {
        try
        {
            await next.Invoke(token);
        }
        catch (Exception e)
        {
            logger.LogError("Error while publishing context: {@Context}, error: {@Error}", context, e);
            throw;
        }
    }
}
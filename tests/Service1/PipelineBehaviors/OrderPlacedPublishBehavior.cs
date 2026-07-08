using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Delegates;
using Service1.Dtos.Orders;

namespace Service1.PipelineBehaviors;

// Publisher-side counterpart to OrderPlacedLoggingBehavior: runs on the way out, before the
// message is handed to the transport.
public sealed class OrderPlacedPublishBehavior(ILogger<OrderPlacedPublishBehavior> logger) :
    IPublisherPipelineBehavior<OrderPlaced>
{
    public async Task PublishAsync(OrderPlaced message, IPublisherContext context, PublisherHandlerDelegate next,
        CancellationToken token = default)
    {
        logger.LogInformation("Publisher pipeline behavior for OrderPlaced: {@Message}, delay: {@Delay}",
            message, context.Delay);
        await next.Invoke(token);
    }
}

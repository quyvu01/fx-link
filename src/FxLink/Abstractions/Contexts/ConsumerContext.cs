using FxLink.Statics;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Abstractions.Contexts;

public class ConsumerContext<TMessage> : AbstractContext, IConsumerContext<TMessage> where TMessage : class
{
    internal ConsumerContext(TMessage message, Guid? requesterId, Guid correlationId,
        IHeaders headers) : base(correlationId, headers)
    {
        RequesterId = requesterId;
        Message = message;
    }

    public ConsumerContext(TMessage message, Guid? requesterId, IContext context)
        : this(message, requesterId, context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    public Guid? RequesterId { get; }
    public TMessage Message { get; }

    public async Task ResponseAsync<TResponse>(TResponse message, CancellationToken token = default)
        where TResponse : class
    {
        var services = ConsumerAmbient.Services;
        var client = services.GetService<IClientConnector<Result>>();
        if (client is null || RequesterId is not { } requesterId) return;
        await client.SendAsync(Result.Success(message), new ResponseContext(requesterId, this), token);
    }

    public async Task PublishAsync<T>(T message, IPublisherContext context, CancellationToken token = default)
        where T : class
    {
        var services = ConsumerAmbient.Services;
        var publisher = services.GetRequiredService<IPublisher>();
        await publisher.PublishAsync(message, context, token);
    }

    public async Task PublishAsync<T>(T message, CancellationToken token = default) where T : class =>
        await PublishAsync(message, new PublisherContext(this), token);
}
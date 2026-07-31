using FxLink.Extensions;
using FxLink.Serialization;
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

    public Task ResponseAsync<TResponse>(object message, CancellationToken token = default) where TResponse : class =>
        ResponseAsync(MessageContractActivator.CreateFrom<TResponse>(message), token);

    public async Task PublishAsync<T>(T message, Action<IPublisherContext> contextOptions,
        CancellationToken token = default) where T : class
    {
        var services = ConsumerAmbient.Services;
        var publisher = services.GetRequiredService<IPublisher>();
        publisher.SetContext(this);
        await publisher.PublishAsync(message, contextOptions, token);
    }

    public async Task PublishAsync<T>(T message, CancellationToken token = default) where T : class
    {
        var services = ConsumerAmbient.Services;
        var publisher = services.GetRequiredService<IPublisher>();
        publisher.SetContext(this);
        await publisher.PublishAsync(message, null, token);
    }
}
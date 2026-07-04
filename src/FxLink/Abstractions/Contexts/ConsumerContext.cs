using FxLink.Statics;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Abstractions.Contexts;

public sealed class ConsumerContext<TMessage>(
    TMessage message,
    Guid? requesterId,
    Guid correlationId,
    Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IConsumerContext<TMessage> where TMessage : class
{
    public ConsumerContext(TMessage message,
        Guid? requesterId, IContext context) : this(message, requesterId, context.CorrelationId, context.Headers)
    {
    }

    public Guid? RequesterId { get; } = requesterId;
    public TMessage Message { get; } = message;

    public async Task ResponseAsync<TResponse>(TResponse message, CancellationToken token = default)
        where TResponse : class
    {
        var services = ServiceProviderAmbient.Services;
        var client = services.GetService<IClient<Result>>();
        if (client is null || RequesterId is not { } requesterId) return;
        await client.SendAsync(Result.Success(message), new ResponseContext(requesterId, this), token);
    }

    public async Task PublishAsync<T>(T message, IPublisherContext context, CancellationToken token = default)
        where T : class
    {
        var services = ServiceProviderAmbient.Services;
        var publisher = services.GetRequiredService<IPublisher>();
        await publisher.PublishAsync(message, context, token);
    }

    public async Task PublishAsync<T>(T message, CancellationToken token = default) where T : class =>
        await PublishAsync(message, new PublisherContext(this), token);
}
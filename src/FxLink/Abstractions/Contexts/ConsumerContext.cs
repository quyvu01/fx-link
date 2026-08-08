using System.Collections.Concurrent;
using FxLink.Exceptions;
using FxLink.Extensions;
using FxLink.Serialization;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Abstractions.Contexts;

public class ConsumerContext<TMessage> : AbstractContext, IConsumerContext<TMessage>
    where TMessage : class
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _contextPayloads = [];

    internal ConsumerContext(TMessage message, Guid? requesterId, Guid correlationId, IHeaders headers,
        DateTime? sentTime = null, IHostInfo hostInfo = null) : base(correlationId, headers, sentTime, hostInfo)
    {
        RequesterId = requesterId;
        Message = message;
    }

    public ConsumerContext(TMessage message, Guid? requesterId, IContext context)
        : this(message, requesterId, context.CorrelationId, new HeaderBag(context.Headers),
            context.SentTime, context.HostInfo)
    {
    }

    public Guid? RequesterId { get; }
    public TMessage Message { get; }

    public async Task ResponseAsync<TResponse>(TResponse message, CancellationToken token = default)
        where TResponse : class
    {
        var services = GetPayload<IServiceProvider>();
        var client = services.GetService<IClientConnector<Result<TResponse>>>();
        if (client is null || RequesterId is not { } requesterId) return;
        await client.SendAsync(Result<TResponse>.Success(message), new ResponseContext(requesterId, this), token);
    }

    public Task ResponseAsync<TResponse>(object message, CancellationToken token = default) where TResponse : class =>
        ResponseAsync(MessageContractActivator.CreateFrom<TResponse>(message), token);

    public async Task PublishAsync<T>(T message, Action<IPublisherContext> contextOptions,
        CancellationToken token = default) where T : class
    {
        var services = GetPayload<IServiceProvider>();
        var publisher = services.GetRequiredService<IPublisher>();
        publisher.SetContext(this);
        await publisher.PublishAsync(message, contextOptions, token);
    }

    public async Task PublishAsync<T>(T message, CancellationToken token = default) where T : class
        => await PublishAsync(message, null, token);

    public T GetPayload<T>()
    {
        if (!_contextPayloads.TryGetValue(typeof(T), out var payload))
            throw new FxLinkException.ContextPayloadNotFound(typeof(T));
        return (T)payload.Value ?? throw new FxLinkException.ContextPayloadNotFound(typeof(T));
    }

    public void SetPayload<T>(T payload) => _contextPayloads[typeof(T)] = new Lazy<object>(() => payload);
}
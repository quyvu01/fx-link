using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Exceptions;
using FxLink.Extensions;
using FxLink.Serialization;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Contexts;

public class ConsumerContext<TMessage> : AbstractContext, IConsumerContext<TMessage>
    where TMessage : class
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _contextPayloads = [];

    internal ConsumerContext(TMessage message, IHeaders headers, Guid correlationId, Guid? requesterId,
        DateTime? sentTime = null, IHostInfo hostInfo = null, TimeSpan? timeToLive = null, Guid? messageId = null)
        : base(headers, correlationId, sentTime, hostInfo, messageId)
    {
        RequesterId = requesterId;
        Message = message;
        TimeToLive = timeToLive;
    }

    public ConsumerContext(TMessage message, IContext context, Guid? requesterId)
        : this(message, new HeaderBag(context.Headers), context.CorrelationId, requesterId,
            context.SentTime, context.HostInfo, (context as IConsumerContext)?.TimeToLive, context.MessageId)
    {
    }

    public Guid? RequesterId { get; }
    public TMessage Message { get; }
    public TimeSpan? TimeToLive { get; }

    public async Task ResponseAsync<TResponse>(TResponse message, CancellationToken token = default)
        where TResponse : class
    {
        var services = GetPayload<IServiceProvider>();
        var client = services.GetService<IClientConnector<Result<TResponse>>>();
        if (client is null || RequesterId is not { } requesterId) return;
        await client.SendAsync(Result<TResponse>.Success(message), new ResponseContext(this, requesterId), token);
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
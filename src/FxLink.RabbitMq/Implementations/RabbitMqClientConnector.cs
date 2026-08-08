using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Entities;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Entities;
using FxLink.RabbitMq.Extensions;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Implementations;

internal abstract class AbstractRabbitMqConnector
{
    public abstract Task ProcessMessageReceivedAsync(BasicDeliverEventArgs args, Type consumerType);
    public abstract Task ProcessResponseMessageAsync(BasicDeliverEventArgs args);
}

internal class RabbitMqClientConnector<TMessage>(
    IRabbitMqClient client,
    IServiceProvider services,
    IInMemoryResponseSetter inMemoryResponseSetter) :
    AbstractRabbitMqConnector, IClientConnector<TMessage> where TMessage : class
{
    private readonly IDelayMessageProvider _delayMessageProvider = services.GetService<IDelayMessageProvider>();

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        var deliveryKind = context.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey);
        var delay = (context as IPublisherContext)?.DelayTime;

        if (deliveryKind == DistributedConfigurators.DeliveryKinds.Delay)
        {
            if (_delayMessageProvider is null)
                throw new InvalidOperationException(
                    $"Cannot send a delayed {typeof(TMessage).Name}: no IDelayMessageProvider is registered. " +
                    "Call IConfigurator.UseRabbitMqDelayScheduler() (or register a custom IDelayMessageProvider) " +
                    "before publishing delayed messages.");
            await _delayMessageProvider.PublishDelayedAsync(message, context,
                (long)(delay ?? TimeSpan.Zero).TotalMilliseconds, token);
            return;
        }

        var props = new BasicProperties
            { CorrelationId = context.CorrelationId.ToString(), Type = typeof(TMessage).AssemblyQualifiedName };
        switch (context)
        {
            case IRequestContext requestContext:
            {
                props.ReplyTo = client.ReplyQueueName;
                if (requestContext.TimeToLive is { } ttl) props.Expiration = ((long)ttl.TotalMilliseconds).ToString();
                break;
            }
            case IPublisherContext
                when deliveryKind == DistributedConfigurators.DeliveryKinds.Retry && delay is { } retryDelay:
                props.Expiration = ((long)retryDelay.TotalMilliseconds).ToString();
                break;
            case IPublisherContext publisherContext:
            {
                if (publisherContext.TimeToLive is { } ttl)
                    props.Expiration = ((long)ttl.TotalMilliseconds).ToString();
                break;
            }
        }

        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageBytes = Encoding.UTF8.GetBytes(serializedMessage);
        var routingKey = string.Empty;
        if (context is IResponseContext responseContext)
            routingKey = responseContext.Headers.Get<string>(DistributedConfigurators.Headers.ReplyToKey);

        var exchangeName = GetExchangeName(context, deliveryKind);
        await client.PublishMessageAsync(new MessagePublisher(exchangeName, routingKey, props, messageBytes), token);
    }

    private static string GetExchangeName(IContext context, string deliveryKind)
    {
        if (context is IResponseContext) return string.Empty;
        return deliveryKind switch
        {
            DistributedConfigurators.DeliveryKinds.Retry => typeof(TMessage).GetRetryExchangeName(),
            DistributedConfigurators.DeliveryKinds.DeadLetter => typeof(TMessage).GetDeadLetterExchangeName(),
            _ => typeof(TMessage).GetExchangeName()
        };
    }

    public override async Task ProcessMessageReceivedAsync(BasicDeliverEventArgs args, Type consumerType)
    {
        var messageType = args.BasicProperties.Type;
        if (typeof(TMessage).AssemblyQualifiedName != messageType) return;
        var bodyAsJson = Encoding.UTF8.GetString(args.Body.Span);
        var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<TMessage>>(bodyAsJson,
            DistributedConfigurators.JsonSerializerOptions);

        if (envelope is null) return;

        using var scope = services.CreateScope();
        var serverConnector = scope.ServiceProvider.GetRequiredService<IConsumerConnector<TMessage>>();
        var headers = envelope.Context.Headers;
        if (args.BasicProperties.ReplyTo is { Length: > 0 } replyTo)
            headers.Set(DistributedConfigurators.Headers.ReplyToKey, replyTo);
        var consumerContext = new ConsumerContext<TMessage>(envelope.Message, envelope.Context.RequesterId,
            envelope.Context.CorrelationId, headers, envelope.Context.SentTime, envelope.Context.HostInfo);
        await serverConnector.ConsumeAsync(consumerContext, consumerType, args.CancellationToken);
    }

    private static readonly
        ConcurrentDictionary<Type, Action<RabbitMqClientConnector<TMessage>, string, CancellationToken>>
        ProcessResponseDelegateCache = new();

    public override Task ProcessResponseMessageAsync(BasicDeliverEventArgs args)
    {
        var messageTypeAsString = args.BasicProperties.Type;
        if (string.IsNullOrEmpty(messageTypeAsString)) return Task.CompletedTask;
        var messageType = Type.GetType(messageTypeAsString);
        if (messageType is null || !messageType.IsGenericType ||
            messageType.GetGenericTypeDefinition() != typeof(Result<>)) return Task.CompletedTask;
        var responseType = messageType.GetGenericArguments()[0];
        var jsonBody = Encoding.UTF8.GetString(args.Body.Span);

        var processDelegate = ProcessResponseDelegateCache
            .GetOrAdd(responseType, BuildProcessResponseDelegate);
        processDelegate.Invoke(this, jsonBody, args.CancellationToken);
        return Task.CompletedTask;
    }

    private static Action<RabbitMqClientConnector<TMessage>, string, CancellationToken> BuildProcessResponseDelegate(
        Type responseType)
    {
        var openMethod = typeof(RabbitMqClientConnector<TMessage>).GetMethod(
            nameof(ProcessResponseMessageInternalAsync),
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var closedMethod = openMethod.MakeGenericMethod(responseType);

        var instanceParam = Expression.Parameter(typeof(RabbitMqClientConnector<TMessage>), "instance");
        var jsonParam = Expression.Parameter(typeof(string), "json");
        var tokenParam = Expression.Parameter(typeof(CancellationToken), "token");
        var call = Expression.Call(instanceParam, closedMethod, jsonParam, tokenParam);

        return Expression.Lambda<Action<RabbitMqClientConnector<TMessage>, string, CancellationToken>>(
            call, instanceParam, jsonParam, tokenParam).Compile();
    }

    private void ProcessResponseMessageInternalAsync<TResponse>(string json, CancellationToken token = default)
        where TResponse : class
    {
        var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<Result<TResponse>>>(json,
            DistributedConfigurators.JsonSerializerOptions);
        if (envelope?.Context.RequesterId is not { } requesterId) return;
        inMemoryResponseSetter.TrySetResult(requesterId, new MessageData<Result<TResponse>>(envelope.Message,
            new ResponseContext(requesterId, envelope.Context.CorrelationId, envelope.Context.Headers), token));
    }
}
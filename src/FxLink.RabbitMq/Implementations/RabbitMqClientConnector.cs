using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
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
    IServiceProvider serviceProvider) :
    AbstractRabbitMqConnector, IClientConnector<TMessage> where TMessage : class
{
    private readonly IDelayMessageProvider _delayMessageProvider = serviceProvider.GetService<IDelayMessageProvider>();

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
            // The response leg previously got no Expiration at all — a response could sit in the
            // reply queue indefinitely even after the requester already timed out. TimeToLive here
            // is carried forward from the originating request (see ResponseContext), so the reply
            // expires under the same wire-level budget the request itself was published with.
            case IResponseContext { TimeToLive: { } ttl }:
                props.Expiration = ((long)ttl.TotalMilliseconds).ToString();
                break;
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

    private string GetExchangeName(IContext context, string deliveryKind)
    {
        if (context is IResponseContext) return string.Empty;
        var exchangeName = GetExchangeName();
        return deliveryKind switch
        {
            DistributedConfigurators.DeliveryKinds.Retry => exchangeName.GetRetryExchangeName(),
            DistributedConfigurators.DeliveryKinds.DeadLetter => exchangeName.GetDeadLetterExchangeName(),
            _ => exchangeName
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

        using var scope = serviceProvider.CreateScope();
        var serverConnector = scope.ServiceProvider.GetRequiredService<IConsumerConnector<TMessage>>();
        var headers = envelope.Context.Headers;
        if (args.BasicProperties.ReplyTo is { Length: > 0 } replyTo)
            headers.Set(DistributedConfigurators.Headers.ReplyToKey, replyTo);
        var consumerContext = new ConsumerContext<TMessage>(envelope.Message, envelope.Context.RequesterId,
            envelope.Context.CorrelationId, headers, envelope.Context.SentTime, envelope.Context.HostInfo,
            envelope.Context.TimeToLive);
        await serverConnector.ConsumeAsync(consumerContext, consumerType, args.CancellationToken);
    }

    private static readonly ConcurrentDictionary<string, Type> MessageResponseProcessors = new();

    public override Task ProcessResponseMessageAsync(BasicDeliverEventArgs args)
    {
        var messageTypeAsString = args.BasicProperties.Type;

        if (string.IsNullOrEmpty(messageTypeAsString)) return Task.CompletedTask;
        var serviceType = MessageResponseProcessors
            .GetOrAdd(messageTypeAsString, static msg =>
            {
                var messageType = Type.GetType(msg);
                if (messageType is null || !messageType.IsGenericType ||
                    messageType.GetGenericTypeDefinition() != typeof(Result<>)) return null;
                var responseType = messageType.GetGenericArguments()[0];
                return typeof(IWireResultDispatcher<>).MakeGenericType(responseType);
            });
        if (serviceType is null) return Task.CompletedTask;
        var jsonBody = Encoding.UTF8.GetString(args.Body.Span);
        var wireResultDispatcher = serviceProvider.GetRequiredService(serviceType) as WireResultDispatcher;
        wireResultDispatcher?.SetResult(jsonBody, args.CancellationToken);
        return Task.CompletedTask;
    }

    private string GetExchangeName()
    {
        var messageDefinition = serviceProvider.GetService<IMessageDefinition<TMessage>>();
        return messageDefinition is not IMessageDefinition definition
            ? typeof(TMessage).GetExchangeName()
            : definition.MessageConfigurator.GetName();
    }
}
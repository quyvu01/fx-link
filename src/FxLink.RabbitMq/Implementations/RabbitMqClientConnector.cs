using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Entities;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Extensions;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Implementations;

internal class RabbitMqClientConnector<TMessage> :
    IClientConnector<TMessage> where TMessage : class
{
    private readonly IRabbitMqClient _client;

    public RabbitMqClientConnector(IRabbitMqClient client, IServiceProvider services,
        IInMemoryResponseSetter inMemoryResponseSetter)
    {
        _client = client;
        client.MessageConsumed(async (sender, e, consumerType, ct) =>
        {
            var messageType = e.BasicProperties.Type;
            if (typeof(TMessage).AssemblyQualifiedName != messageType) return;
            var bodyAsJson = Encoding.UTF8.GetString(e.Body.Span);
            var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<TMessage>>(bodyAsJson,
                DistributedConfigurators.JsonSerializerOptions);

            if (envelope is null) return;

            using var scope = services.CreateScope();
            var serverConnector = scope.ServiceProvider.GetRequiredService<IConsumerConnector<TMessage>>();
            var headers = envelope.Context.Headers;
            if (e.BasicProperties.ReplyTo is { Length: > 0 } replyTo)
                headers[DistributedConfigurators.Headers.ReplyToKey] = replyTo;
            var consumerContext = new ConsumerContext<TMessage>(envelope.Message, envelope.Context.RequesterId,
                envelope.Context.CorrelationId, headers);
            await serverConnector.ConsumeAsync(consumerContext, consumerType, ct);
            var channel = ((AsyncEventingBasicConsumer)sender).Channel;
            await channel.BasicAckAsync(e.DeliveryTag, true, ct);
        });

        client.MessageRequesterConsumer(async (sender, e, ct) =>
        {
            var messageType = e.BasicProperties.Type;
            if (typeof(Result).AssemblyQualifiedName != messageType) return;
            var bodyAsJson = Encoding.UTF8.GetString(e.Body.Span);
            var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<Result>>(bodyAsJson,
                DistributedConfigurators.JsonSerializerOptions);
            if (envelope?.Context.RequesterId is not { } requesterId) return;
            inMemoryResponseSetter.TrySetResult(requesterId, new MessageData<Result>(envelope.Message,
                new ResponseContext(requesterId, envelope.Context.CorrelationId, envelope.Context.Headers), ct));
            var channel = ((AsyncEventingBasicConsumer)sender).Channel;
            await channel.BasicAckAsync(e.DeliveryTag, true, ct);
        });
    }

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        var props = new BasicProperties
            { CorrelationId = context.CorrelationId.ToString(), Type = typeof(TMessage).AssemblyQualifiedName };
        if (context is IRequestContext) props.ReplyTo = _client.ReplyQueueName;
        var messageType = GetMessageType(context);
        switch (messageType)
        {
            case DistributedConfigurators.MessageTypes.Retry:
                props.Expiration = GetTimeToLiveFromHeader(context.Headers).ToString();
                break;
            case DistributedConfigurators.MessageTypes.Delay:
                props.Headers = new Dictionary<string, object>(context.Headers);
                props.Headers["x-delay"] = GetDelayTimeFromHeader(context.Headers);
                break;
        }

        var envelope = new Envelope<TMessage>(message, context);
        var messageSerialize = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageBytes = Encoding.UTF8.GetBytes(messageSerialize);
        var exchangeName = GetExchangeName(context, messageType);
        var routingKey = string.Empty;
        if (context is IResponseContext responseContext && responseContext.Headers
                .TryGetValue(DistributedConfigurators.Headers.ReplyToKey, out var replyToAsObject))
            routingKey = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(replyToAsObject));

        if (_client.Channel is { } channel)
            await channel.BasicPublishAsync(exchangeName, routingKey: routingKey,
                mandatory: true, basicProperties: props, body: messageBytes, cancellationToken: token);
    }

    private static string GetMessageType(IContext context)
    {
        if (!context.Headers.TryGetValue(DistributedConfigurators.Headers.MessageTypeKey, out var messageTypeObject))
            return null;
        var messageTypeJson = JsonSerializer.Serialize(messageTypeObject);
        return JsonSerializer.Deserialize<string>(messageTypeJson);
    }

    private static string GetExchangeName(IContext context, string messageType)
    {
        if (context is IResponseContext) return string.Empty;
        return messageType switch
        {
            DistributedConfigurators.MessageTypes.Retry => typeof(TMessage).GetRetryExchangeName(),
            DistributedConfigurators.MessageTypes.DeadLetter => typeof(TMessage).GetDeadLetterExchangeName(),
            DistributedConfigurators.MessageTypes.Delay => typeof(TMessage).GetDelayExchangeName(),
            _ => typeof(TMessage).GetExchangeName()
        };
    }

    private static long GetTimeToLiveFromHeader(Dictionary<string, object> headers)
    {
        if (!headers.TryGetValue(DistributedConfigurators.Headers.TimeToLiveKey, out var timeToLiveObject)) return 0;
        return double.TryParse(JsonSerializer.Serialize(timeToLiveObject), out var timeToLive) ? (long)timeToLive : 0;
    }

    private static long GetDelayTimeFromHeader(Dictionary<string, object> headers)
    {
        if (!headers.TryGetValue(DistributedConfigurators.Headers.DelayInMsKey, out var delayInMs)) return 0;
        return double.TryParse(JsonSerializer.Serialize(delayInMs), out var delay) ? (long)delay : 0;
    }
}
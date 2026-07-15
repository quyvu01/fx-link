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
            var consumerContext = new ConsumerContext<TMessage>(envelope.Message, envelope.Context.RequesterId,
                envelope.Context.CorrelationId, envelope.Context.Headers) { RoutingKey = e.BasicProperties.ReplyTo };
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
        if (messageType == DistributedConfigurators.MessageTypeRetry)
            props.Expiration = GetTimeToLiveFromHeader(context.Headers).ToString();
        var envelope = new Envelope<TMessage>(message, context);
        var messageSerialize = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageBytes = Encoding.UTF8.GetBytes(messageSerialize);
        var exchangeName = GetExchangeName(context, messageType);
        var routingKey = context is IResponseContext responseContext ? responseContext.RoutingKey : string.Empty;
        if (_client.Channel is not null)
            await _client.Channel.BasicPublishAsync(exchangeName, routingKey: routingKey,
                mandatory: true, basicProperties: props, body: messageBytes, cancellationToken: token);
    }

    private static string GetMessageType(IContext context)
    {
        if (!context.Headers.TryGetValue(DistributedConfigurators.MessageTypeKey, out var messageTypeObject))
            return null;
        var messageTypeJson = JsonSerializer.Serialize(messageTypeObject);
        return JsonSerializer.Deserialize<string>(messageTypeJson);
    }

    private static string GetExchangeName(IContext context, string messageType)
    {
        if (context is IResponseContext) return string.Empty;
        return messageType switch
        {
            DistributedConfigurators.MessageTypeRetry => typeof(TMessage).GetRetryExchangeName(),
            DistributedConfigurators.MessageTypeDeadLetter => typeof(TMessage).GetDeadLetterExchangeName(),
            _ => typeof(TMessage).GetExchangeName()
        };
    }

    private static long GetTimeToLiveFromHeader(Dictionary<string, object> headers)
    {
        if (!headers.TryGetValue(DistributedConfigurators.TimeToLiveKey, out var timeToLiveObject)) return 0;
        return double.TryParse(JsonSerializer.Serialize(timeToLiveObject), out var timeToLive) ? (long)timeToLive : 0;
    }
}
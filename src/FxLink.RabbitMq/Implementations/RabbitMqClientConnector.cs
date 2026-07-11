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

namespace FxLink.RabbitMq.Implementations;

internal class RabbitMqClientConnector<TMessage> :
    IClientConnector<TMessage> where TMessage : class
{
    private readonly IRabbitMqClient _client;

    public RabbitMqClientConnector(IRabbitMqClient client, IServiceProvider services,
        IInMemoryResponseSetter inMemoryResponseSetter)
    {
        _client = client;
        client.MessageConsumed(async (_, e, ct) =>
        {
            var messageType = e.BasicProperties.Type;
            if (typeof(TMessage).AssemblyQualifiedName != messageType) return;
            var bodyAsJson = Encoding.UTF8.GetString(e.Body.Span);
            var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<TMessage>>(bodyAsJson,
                DistributedConfigurators.JsonSerializerOptions);

            if (envelope is null) return;

            using var scope = services.CreateScope();
            var serverConnector = scope.ServiceProvider.GetRequiredService<IServerConnector<TMessage>>();
            var consumerContext = new ConsumerContext<TMessage>(envelope.Message, envelope.Context.RequesterId,
                envelope.Context.CorrelationId, envelope.Context.Headers) { RoutingKey = e.BasicProperties.ReplyTo };
            await serverConnector.ConsumeAsync(consumerContext, ct);
        });

        client.MessageRequesterConsumer((_, e, ct) =>
        {
            var messageType = e.BasicProperties.Type;
            if (typeof(Result).AssemblyQualifiedName != messageType) return Task.CompletedTask;
            var bodyAsJson = Encoding.UTF8.GetString(e.Body.Span);
            var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<Result>>(bodyAsJson,
                DistributedConfigurators.JsonSerializerOptions);
            if (envelope?.Context.RequesterId is not { } requesterId) return Task.CompletedTask;
            inMemoryResponseSetter.TrySetResult(requesterId,
                new MessageData<Result>(envelope.Message,
                    new ResponseContext(requesterId, envelope.Context.CorrelationId, envelope.Context.Headers), ct));
            return Task.CompletedTask;
        });
    }

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        var props = new BasicProperties
        {
            CorrelationId = context.CorrelationId.ToString(),
            Type = typeof(TMessage).AssemblyQualifiedName,
        };
        if (context is IRequestContext) props.ReplyTo = _client.ReplyQueueName;
        props.Headers ??= new Dictionary<string, object>();
        var envelope = new Envelope<TMessage>(message, context);
        var messageSerialize = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageBytes = Encoding.UTF8.GetBytes(messageSerialize);
        var exchangeName = context is IResponseContext ? string.Empty : typeof(TMessage).GetExchangeName();
        var routingKey = context is IResponseContext responseContext ? responseContext.RoutingKey : string.Empty;
        if (_client.Channel is not null)
            await _client.Channel.BasicPublishAsync(exchangeName, routingKey: routingKey,
                mandatory: true, basicProperties: props, body: messageBytes, cancellationToken: token);
    }
}
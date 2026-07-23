using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Entities;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Entities;
using FxLink.RabbitMq.Extensions;
using RabbitMQ.Client;

namespace FxLink.RabbitMq.Implementations;

/// <summary>
/// Default IDelayMessageProvider for FxLink.RabbitMq, backed by the RabbitMQ
/// delayed-message-exchange plugin. Registered via IConfigurator.UseRabbitMqDelayScheduler().
/// </summary>
internal sealed class RabbitMqScheduleExchangeProvider(IRabbitMqClient client) :
    IDelayMessageProvider, IRabbitMqDelayTopology
{
    private const string DelayedMessageExchangeType = "x-delayed-message";

    public async Task DeclareTopologyAsync(IChannel channel, Type messageType, string queueName,
        CancellationToken cancellationToken = default)
    {
        var delayExchange = messageType.GetDelayExchangeName();
        await channel.ExchangeDeclareAsync(delayExchange, DelayedMessageExchangeType, durable: false,
            arguments: new Dictionary<string, object> { ["x-delayed-type"] = ExchangeType.Fanout },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queueName, delayExchange, string.Empty,
            cancellationToken: cancellationToken);
    }

    public Task PublishDelayedAsync<TMessage>(TMessage message, IContext context, long delayInMs,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        var props = new BasicProperties
        {
            CorrelationId = context.CorrelationId.ToString(),
            Headers = new Dictionary<string, object>(context.Headers) { ["x-delay"] = delayInMs },
            Type = typeof(TMessage).AssemblyQualifiedName
        };

        var routingKey = string.Empty;
        if (context is IResponseContext responseContext && responseContext.Headers
                .TryGetValue(DistributedConfigurators.Headers.ReplyToKey, out var replyToAsObject))
            routingKey = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(replyToAsObject));

        var envelope = new Envelope<TMessage>(message, context);
        var messageSerialize = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageBytes = Encoding.UTF8.GetBytes(messageSerialize);

        var delayExchange = typeof(TMessage).GetDelayExchangeName();
        // mandatory: false — the delayed-message-exchange plugin always reports NO_ROUTE for a
        // delayed publish (routing only happens after the delay elapses), even when the message
        // is delivered correctly. See MessagePublisher.Mandatory for details.
        return client.PublishMessageAsync(
            new MessagePublisher(delayExchange, routingKey, props, messageBytes, Mandatory: false),
            cancellationToken);
    }
}

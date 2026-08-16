using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
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
internal sealed class RabbitMqScheduleExchangeProvider(IRabbitMqClient client, IServiceProvider serviceProvider) :
    IDelayMessageProvider, IRabbitMqDelayTopology
{
    private const string DelayedMessageExchangeType = "x-delayed-message";

    public async Task DeclareTopologyAsync(IChannel channel, Type messageType, string queueName,
        CancellationToken cancellationToken = default)
    {
        var exchangeName = GetExchangeName(messageType);
        var delayExchange = exchangeName.GetDelayExchangeName();
        await channel.ExchangeDeclareAsync(delayExchange, DelayedMessageExchangeType, durable: false,
            arguments: new Dictionary<string, object> { ["x-delayed-type"] = ExchangeType.Fanout },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queueName, delayExchange, string.Empty,
            cancellationToken: cancellationToken);
    }

    public Task PublishDelayedAsync<TMessage>(TMessage message, IContext context, long delayInMs,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        var nativeHeaders =
            context.Headers.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        nativeHeaders["x-delay"] = delayInMs;
        var props = new BasicProperties
        {
            CorrelationId = context.CorrelationId.ToString(),
            Headers = nativeHeaders,
            Type = typeof(TMessage).AssemblyQualifiedName
        };

        var routingKey = string.Empty;
        if (context is IResponseContext responseContext)
            routingKey = responseContext.Headers.Get<string>(DistributedConfigurators.Headers.ReplyToKey);

        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageBytes = Encoding.UTF8.GetBytes(serializedMessage);
        var exchangeName = GetExchangeName(typeof(TMessage));
        var delayExchange = exchangeName.GetDelayExchangeName();
        // mandatory: false — the delayed-message-exchange plugin always reports NO_ROUTE for a
        // delayed publish (routing only happens after the delay elapses), even when the message
        // is delivered correctly. See MessagePublisher.Mandatory for details.
        return client.PublishMessageAsync(
            new MessagePublisher(delayExchange, routingKey, props, messageBytes, Mandatory: false),
            cancellationToken);
    }

    private string GetExchangeName(Type messageType)
    {
        var definition = serviceProvider.GetService(typeof(IMessageDefinition<>).MakeGenericType(messageType));
        return definition is not IMessageDefinition messageDefinition
            ? messageType.GetExchangeName()
            : messageDefinition.MessageConfigurator.GetName();
    }
}
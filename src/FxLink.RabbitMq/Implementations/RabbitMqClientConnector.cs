using System.Collections.Concurrent;
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

internal class RabbitMqClientConnector<TMessage> :
    IClientConnector<TMessage> where TMessage : class
{
    private readonly IRabbitMqClient _client;

    // Optional: only needed for the Delay message type, and only resolved if a delay provider
    // was actually registered (e.g. via IConfigurator.UseRabbitMqDelayScheduler()). No default
    // is registered, so apps that never send Delay messages never need to configure one.
    private readonly IDelayMessageProvider _delayMessageProvider;

    // A process that only publishes TMessage (no local consumer) never runs the declare loop in
    // RabbitMqClient.StartAsync, so the target exchange may not exist yet on this broker. Declare
    // it ourselves before the first publish, cached per exchange name so it only costs one round
    // trip — matches how MassTransit always declares the exchange on publish regardless of whether
    // a local consumer exists.
    private readonly ConcurrentDictionary<string, Lazy<Task>> _declaredExchanges = new();

    public RabbitMqClientConnector(IRabbitMqClient client, IServiceProvider services,
        IInMemoryResponseSetter inMemoryResponseSetter)
    {
        _client = client;
        _delayMessageProvider = services.GetService<IDelayMessageProvider>();
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
        var messageType = GetMessageType(context);

        if (messageType == DistributedConfigurators.MessageTypes.Delay)
        {
            if (_delayMessageProvider is null)
                throw new InvalidOperationException(
                    $"Cannot send a delayed {typeof(TMessage).Name}: no IDelayMessageProvider is registered. " +
                    "Call IConfigurator.UseRabbitMqDelayScheduler() (or register a custom IDelayMessageProvider) " +
                    "before publishing delayed messages.");
            var delayInMs = GetDelayTimeFromHeader(context.Headers);
            await _delayMessageProvider.PublishDelayedAsync(message, context, delayInMs, token);
            return;
        }

        var props = new BasicProperties
            { CorrelationId = context.CorrelationId.ToString(), Type = typeof(TMessage).AssemblyQualifiedName };
        if (context is IRequestContext) props.ReplyTo = _client.ReplyQueueName;

        var envelope = new Envelope<TMessage>(message, context);
        var messageSerialize = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageBytes = Encoding.UTF8.GetBytes(messageSerialize);
        var routingKey = string.Empty;
        if (context is IResponseContext responseContext && responseContext.Headers
                .TryGetValue(DistributedConfigurators.Headers.ReplyToKey, out var replyToAsObject))
            routingKey = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(replyToAsObject));

        var exchangeName = GetExchangeName(context, messageType);
        if (!string.IsNullOrEmpty(exchangeName)) await EnsureExchangeDeclaredAsync(exchangeName);

        await _client.PublishMessageAsync(new MessagePublisher(exchangeName, routingKey, props, messageBytes), token);
    }

    private Task EnsureExchangeDeclaredAsync(string exchangeName)
    {
        var lazy = _declaredExchanges.GetOrAdd(exchangeName,
            name => new Lazy<Task>(() => _client.DeclareExchangeAsync(name)));
        if (lazy.Value.IsFaulted)
            // Don't let a transient failure permanently poison this exchange for the process
            // lifetime — drop the cached attempt so the next publish retries the declare.
            _declaredExchanges.TryRemove(exchangeName, out _);
        return lazy.Value;
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
            DistributedConfigurators.MessageTypes.DeadLetter => typeof(TMessage).GetDeadLetterExchangeName(),
            _ => typeof(TMessage).GetExchangeName()
        };
    }

    private static long GetDelayTimeFromHeader(Dictionary<string, object> headers)
    {
        if (!headers.TryGetValue(DistributedConfigurators.Headers.DelayInMsKey, out var delayInMs)) return 0;
        return double.TryParse(JsonSerializer.Serialize(delayInMs), out var delay) ? (long)delay : 0;
    }
}
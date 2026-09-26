using System.Collections.Concurrent;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Nats.Abstractions;
using FxLink.Implementations;
using FxLink.Statics;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Nats.Implementations;

// Analogous to AbstractRabbitMqConnector/AbstractSqsConnector — lets the consume loop resolve
// IClientConnector<>.MakeGenericType(messageType) generically and call into the receive path
// without knowing TMessage at compile time.
internal abstract class AbstractNatsConnector
{
    public abstract Task ProcessMessageReceivedAsync(string body, Type consumerType, string replyTo,
        CancellationToken token = default);

    public abstract Task ProcessResponseMessageAsync(string body, CancellationToken token = default);
}

internal sealed class NatsClientConnector<TMessage>(INatsMessagingClient client, IServiceProvider serviceProvider)
    : AbstractNatsConnector, IClientConnector<TMessage> where TMessage : class
{
    private readonly IDelayMessageProvider _delayMessageProvider = serviceProvider.GetService<IDelayMessageProvider>();

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        var deliveryKind = context.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey);
        var delay = (context as IPublishContext)?.DelayTime;

        if (deliveryKind == DistributedConfigurators.DeliveryKinds.Delay)
        {
            if (_delayMessageProvider is null)
                throw new InvalidOperationException(
                    $"Cannot send a delayed {typeof(TMessage).Name}: no IDelayMessageProvider is registered. " +
                    "Call IConfigurator.UseNatsDelayScheduler() (or register a custom IDelayMessageProvider) " +
                    "before publishing delayed messages.");
            await _delayMessageProvider.PublishDelayedAsync(message, context,
                (long)(delay ?? TimeSpan.Zero).TotalMilliseconds, token);
            return;
        }

        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageTypeName = typeof(TMessage).AssemblyQualifiedName;

        // The response leg: straight to the reply subject the original request carried (see
        // ProcessMessageReceivedAsync's ReplyTo handling), over Core NATS rather than the stream.
        if (context is IResponseContext responseContext)
        {
            var replySubject = responseContext.Headers.Get<string>(DistributedConfigurators.Headers.ReplyToKey);
            if (string.IsNullOrEmpty(replySubject)) return; // no reply address — nothing to send to
            await client.PublishReplyAsync(replySubject, serializedMessage, messageTypeName, token);
            return;
        }

        switch (deliveryKind)
        {
            // Parked on the dead-letter subject, where each consumer's "-deadletter" durable
            // consumer retains it. No dedup id: it carries the original message's MessageId, which
            // JetStream would drop as a duplicate of the delivery that just failed.
            case DistributedConfigurators.DeliveryKinds.DeadLetter:
                await client.PublishAsync(client.GetDeadLetterSubject(typeof(TMessage)), serializedMessage,
                    messageTypeName, token: token);
                return;

            // Broker-side retry: schedule the message to reappear on its own subject once the
            // backoff elapses, freeing this delivery's ack slot immediately — the NATS analogue of
            // RabbitMq's TTL + dead-letter retry queue.
            case DistributedConfigurators.DeliveryKinds.Retry:
                await client.PublishScheduledAsync(client.GetSubject(typeof(TMessage)), serializedMessage,
                    messageTypeName, delay ?? TimeSpan.Zero, token);
                return;
        }

        // Request leg and plain publish alike go through the stream; a request additionally
        // carries this instance's reply subject.
        await client.PublishAsync(client.GetSubject(typeof(TMessage)), serializedMessage, messageTypeName,
            context.MessageId, context is IRequestContext ? client.ReplySubject : null, token);
    }

    public override async Task ProcessMessageReceivedAsync(string body, Type consumerType, string replyTo,
        CancellationToken token = default)
    {
        var messageDefinition = serviceProvider.GetService<IMessageDefinition<TMessage>>();

        var envelope = (messageDefinition is { MessageConfigurator.IsRawJsonSerializer: true }) switch
        {
            false => JsonSerializer.Deserialize<ConsumerContextEnvelope<TMessage>>(body,
                DistributedConfigurators.JsonSerializerOptions),
            _ => new ConsumerContextEnvelope<TMessage>
            {
                Message = JsonSerializer.Deserialize<TMessage>(body,
                    messageDefinition.MessageConfigurator.RawJsonSerializerOptions),
                Context = new ConsumerContextSerializable
                {
                    MessageId = Id.New(),
                    CorrelationId = Id.New(),
                    Headers = new HeaderBag(),
                    SentTime = DateTime.UtcNow,
                    HostInfo = null,
                    TimeToLive = null
                }
            }
        };

        if (envelope is null) return;

        using var scope = serviceProvider.CreateScope();
        var serverConnector = scope.ServiceProvider.GetRequiredService<IConsumerConnector<TMessage>>();
        var headers = envelope.Context.Headers;
        if (replyTo is { Length: > 0 })
            headers.Set(DistributedConfigurators.Headers.ReplyToKey, replyTo);
        var consumerContext = new ConsumeContext<TMessage>(envelope.Message, headers,
            envelope.Context.CorrelationId, envelope.Context.RequesterId, envelope.Context.SentTime,
            envelope.Context.HostInfo, envelope.Context.TimeToLive, envelope.Context.MessageId);
        await serverConnector.ConsumeAsync(consumerContext, consumerType, token);
    }

    private static readonly ConcurrentDictionary<string, Type> ResponseDispatcherTypes = new();

    // Runs on this instance's reply subscription (NatsClient.ReplyAsync). The response's wire type
    // is Result<TResponse>; its body is handed to the matching IWireResultDispatcher<TResponse>,
    // which completes the Requester<> await waiting on that RequesterId.
    public override Task ProcessResponseMessageAsync(string body, CancellationToken token = default)
    {
        var messageTypeName = typeof(TMessage).AssemblyQualifiedName!;
        var serviceType = ResponseDispatcherTypes.GetOrAdd(messageTypeName, static _ =>
        {
            var messageType = typeof(TMessage);
            if (!messageType.IsGenericType || messageType.GetGenericTypeDefinition() != typeof(Result<>))
                return null;
            return typeof(IWireResultDispatcher<>).MakeGenericType(messageType.GetGenericArguments()[0]);
        });
        if (serviceType is null) return Task.CompletedTask;

        ((IWireResultDispatcher)serviceProvider.GetRequiredService(serviceType)).SetResult(body, token);
        return Task.CompletedTask;
    }
}

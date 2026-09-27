using System.Collections.Concurrent;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Azure.ServiceBus.Abstractions;
using FxLink.Azure.ServiceBus.Constants;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Implementations;
using FxLink.Statics;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Azure.ServiceBus.Implementations;

// Analogous to AbstractRabbitMqConnector/AbstractNatsConnector — lets the processors resolve
// IClientConnector<>.MakeGenericType(messageType) generically and call into the receive path without knowing
// TMessage at compile time.
internal abstract class AbstractServiceBusConnector
{
    public abstract Task ProcessMessageReceivedAsync(string body, Type consumerType, string replyTo,
        string replySessionId, CancellationToken token = default);

    public abstract Task ProcessResponseMessageAsync(string body, CancellationToken token = default);
}

internal sealed class ServiceBusClientConnector<TMessage>(
    IServiceBusMessagingClient client,
    IServiceProvider serviceProvider)
    : AbstractServiceBusConnector, IClientConnector<TMessage> where TMessage : class
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
                    "Call IConfigurator.UseServiceBusDelayScheduler() (or register a custom IDelayMessageProvider) " +
                    "before publishing delayed messages.");
            await _delayMessageProvider.PublishDelayedAsync(message, context,
                (long)(delay ?? TimeSpan.Zero).TotalMilliseconds, token);
            return;
        }

        // Dead-letter: settle the delivery being handled into its subscription's native dead-letter queue —
        // no copy is sent, so the queue keeps the original message, plus the reason.
        if (deliveryKind == DistributedConfigurators.DeliveryKinds.DeadLetter)
        {
            var scope = DeliveryScope.Current ?? throw new InvalidOperationException(
                "Dead-lettering needs the message delivery being handled; none is in scope.");
            await scope.DeadLetterAsync(
                context.Headers.Get<string>(DistributedConfigurators.Headers.ExceptionTypeKey) ?? "DeadLettered",
                context.Headers.Get<string>(DistributedConfigurators.Headers.ExceptionMessageKey));
            return;
        }

        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageTypeName = typeof(TMessage).AssemblyQualifiedName;

        // The response leg: into the shared reply queue, addressed to the requesting instance's session.
        if (context is IResponseContext responseContext)
        {
            var replyQueue = responseContext.Headers.Get<string>(DistributedConfigurators.Headers.ReplyToKey);
            var sessionId = responseContext.Headers.Get<string>(ServiceBusConstants.ReplyToSessionKey);
            if (string.IsNullOrEmpty(replyQueue) || string.IsNullOrEmpty(sessionId)) return; // nobody to reply to
            await client.PublishReplyAsync(replyQueue, sessionId, serializedMessage, messageTypeName, token);
            return;
        }

        var topic = client.GetTopicName(typeof(TMessage));

        // Retry: back onto the topic after the backoff, targeted at the subscription that failed so no other
        // consumer of the message sees it again. A fresh MessageId — the original's would be dropped as a duplicate
        // on a topic with duplicate detection — and the failed delivery itself completes normally afterwards.
        if (deliveryKind == DistributedConfigurators.DeliveryKinds.Retry)
        {
            var scope = DeliveryScope.Current ?? throw new InvalidOperationException(
                "A retry needs the message delivery being handled; none is in scope.");
            await client.PublishAsync(topic, serializedMessage, messageTypeName,
                new OutgoingOptions(ScheduledEnqueueTime: DateTimeOffset.UtcNow + (delay ?? TimeSpan.Zero),
                    TargetSubscription: scope.Subscription), token);
            return;
        }

        // Request leg and plain publish alike go through the topic; a request also names the reply queue and this
        // instance's session.
        await client.PublishAsync(topic, serializedMessage, messageTypeName,
            new OutgoingOptions(MessageId: context.MessageId, IsRequest: context is IRequestContext), token);
    }

    public override async Task ProcessMessageReceivedAsync(string body, Type consumerType, string replyTo,
        string replySessionId, CancellationToken token = default)
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
        if (replyTo is { Length: > 0 } && replySessionId is { Length: > 0 })
        {
            headers.Set(DistributedConfigurators.Headers.ReplyToKey, replyTo);
            headers.Set(ServiceBusConstants.ReplyToSessionKey, replySessionId);
        }

        var consumerContext = new ConsumeContext<TMessage>(envelope.Message, headers, envelope.Context.CorrelationId,
            envelope.Context.RequesterId, envelope.Context.SentTime, envelope.Context.HostInfo,
            envelope.Context.TimeToLive, envelope.Context.MessageId);
        await serverConnector.ConsumeAsync(consumerContext, consumerType, token);
    }

    private static readonly ConcurrentDictionary<string, Type> ResponseDispatcherTypes = new();

    // Runs on this instance's reply session. The response's wire type is Result<TResponse>; its body is handed to
    // the matching IWireResultDispatcher<TResponse>, which completes the Requester<> await for that RequesterId.
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

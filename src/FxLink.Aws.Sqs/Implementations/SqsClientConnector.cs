using System.Collections.Concurrent;
using System.Text.Json;
using Amazon.SQS.Model;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Aws.Sqs.Abstractions;
using FxLink.Statics;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Aws.Sqs.Implementations;

// Analogous to AbstractRabbitMqConnector — lets a poll loop resolve
// IClientConnector<>.MakeGenericType(messageType) generically and call into the receive path
// without knowing TMessage at compile time.
internal abstract class AbstractSqsConnector
{
    public abstract Task ProcessMessageReceivedAsync(Message message, Type consumerType,
        CancellationToken token = default);

    public abstract Task ProcessResponseMessageAsync(Message message, CancellationToken token = default);
}

internal sealed class SqsClientConnector<TMessage>(
    ISqsClient client,
    ISqsConnection sqsConnection,
    IServiceProvider serviceProvider) :
    AbstractSqsConnector, IClientConnector<TMessage> where TMessage : class
{
    private readonly IDelayMessageProvider _delayMessageProvider = serviceProvider.GetService<IDelayMessageProvider>();

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        var deliveryKind = context.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey);
        var delay = (context as IPublishContext)?.DelayTime;

        // Retry/dead-letter wire mechanics aren't implemented yet for this transport — fail
        // loudly instead of silently sending as a plain publish.
        if (deliveryKind is DistributedConfigurators.DeliveryKinds.Retry
            or DistributedConfigurators.DeliveryKinds.DeadLetter)
            throw new NotSupportedException($"{deliveryKind} delivery is not implemented yet for the SQS transport.");

        if (deliveryKind == DistributedConfigurators.DeliveryKinds.Delay)
        {
            if (_delayMessageProvider is null)
                throw new InvalidOperationException(
                    $"Cannot send a delayed {typeof(TMessage).Name}: no IDelayMessageProvider is registered. " +
                    "Call IConfigurator.UseSqsDelayScheduler() (or register a custom IDelayMessageProvider) " +
                    "before publishing delayed messages.");
            await _delayMessageProvider.PublishDelayedAsync(message, context,
                (long)(delay ?? TimeSpan.Zero).TotalMilliseconds, token);
            return;
        }

        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageTypeName = typeof(TMessage).AssemblyQualifiedName;

        // The response leg: SNS PublishRequest can't route to one specific requester, so this
        // bypasses the topic entirely and sends directly to the reply queue URL the original
        // request carried (see ProcessMessageReceivedAsync's ReplyTo handling below) — same
        // direct-to-queue approach as SqsDelayMessageProvider, just addressed by the caller
        // instead of by message type.
        if (context is IResponseContext responseContext)
        {
            var replyQueueUrl = responseContext.Headers.Get<string>(DistributedConfigurators.Headers.ReplyToKey);
            if (string.IsNullOrEmpty(replyQueueUrl)) return; // no reply address — nothing to send to
            await sqsConnection.Sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = replyQueueUrl,
                MessageBody = serializedMessage,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["MessageType"] = new() { DataType = "String", StringValue = messageTypeName }
                }
            }, token);
            return;
        }

        // The request leg: published normally through the topic (a request is still a "message
        // type" any registered consumer type can receive, same as a plain publish), just also
        // carrying where to send the reply back to.
        var topicArn = client.GetTopicArn(typeof(TMessage));
        var replyToQueueUrl = context is IRequestContext ? client.ReplyQueueUrl : null;
        await client.PublishAsync(topicArn, serializedMessage, messageTypeName, replyToQueueUrl, token);
    }

    public override async Task ProcessMessageReceivedAsync(Message message, Type consumerType,
        CancellationToken token = default)
    {
        var messageDefinition = serviceProvider.GetService<IMessageDefinition<TMessage>>();

        var envelope = (messageDefinition is { MessageConfigurator.IsRawJsonSerializer: true }) switch
        {
            false => JsonSerializer.Deserialize<ConsumerContextEnvelope<TMessage>>(message.Body,
                DistributedConfigurators.JsonSerializerOptions),
            _ => new ConsumerContextEnvelope<TMessage>
            {
                Message = JsonSerializer.Deserialize<TMessage>(message.Body,
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
        if (message.MessageAttributes.TryGetValue("ReplyTo", out var replyTo) &&
            replyTo.StringValue is { Length: > 0 } replyQueueUrl)
            headers.Set(DistributedConfigurators.Headers.ReplyToKey, replyQueueUrl);
        var consumerContext = new ConsumeContext<TMessage>(envelope.Message, headers, envelope.Context.CorrelationId,
            envelope.Context.RequesterId, envelope.Context.SentTime, envelope.Context.HostInfo,
            envelope.Context.TimeToLive, envelope.Context.MessageId);
        await serverConnector.ConsumeAsync(consumerContext, consumerType, token);
    }

    private static readonly ConcurrentDictionary<string, Type> MessageResponseProcessors = new();

    // Runs on the requester's reply-queue poll loop (SqsClient.ReplyPollLoopAsync). Independent of
    // TMessage — like RabbitMq's equivalent, it only exists as an instance method so it can share
    // the same generic-connector-resolution machinery the regular receive path already uses.
    public override Task ProcessResponseMessageAsync(Message message, CancellationToken token = default)
    {
        if (!message.MessageAttributes.TryGetValue("MessageType", out var attribute) ||
            attribute.StringValue is not { Length: > 0 } messageTypeName)
            return Task.CompletedTask;

        var serviceType = MessageResponseProcessors.GetOrAdd(messageTypeName, static type =>
        {
            var messageType = Type.GetType(type);
            if (messageType is null || !messageType.IsGenericType ||
                messageType.GetGenericTypeDefinition() != typeof(Result<>)) return null;
            var responseType = messageType.GetGenericArguments()[0];
            return typeof(IWireResultDispatcher<>).MakeGenericType(responseType);
        });
        if (serviceType is null) return Task.CompletedTask;

        var wireResultDispatcher = (IWireResultDispatcher)serviceProvider.GetRequiredService(serviceType);
        wireResultDispatcher.SetResult(message.Body, token);
        return Task.CompletedTask;
    }
}
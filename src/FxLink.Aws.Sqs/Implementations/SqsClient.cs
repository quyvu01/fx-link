using System.Collections.Concurrent;
using System.Text.Json;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using FxLink.Abstractions;
using FxLink.Aws.Sqs.Abstractions;
using FxLink.Aws.Sqs.Extensions;
using FxLink.Aws.Sqs.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.Aws.Sqs.Implementations;

internal sealed class SqsClient(
    ISqsConnection sqsConnection,
    IMessageKeys messageKeys,
    IServiceProvider serviceProvider)
    : IMessageBrokerConnector, ISqsClient
{
    private readonly ILogger<SqsClient> _logger = serviceProvider.GetRequiredService<ILogger<SqsClient>>();

    // Populated by StartAsync, read by PublishAsync/the poll loop below — one SNS topic per
    // message type is the fan-out point (mirrors RabbitMq's fanout exchange), one SQS queue per
    // consumer type is what each poll loop reads from.
    private readonly Dictionary<Type, string> _topicArnsByMessageType = new();
    private readonly Dictionary<Type, string> _queueUrlsByConsumerType = new();

    // messageTypeName (AssemblyQualifiedName, from the "MessageType" attribute) -> the closed
    // IClientConnector<> type to resolve from DI — same caching role as RabbitMqClient's
    // _connectorTypeCache.
    private readonly ConcurrentDictionary<string, Type> _connectorTypeCache = new();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var messageKeyMap = messageKeys.GetMessageKeys(); // messageType -> consumerTypes[]

        // One topic per message type — declared before any queue subscribes to it below.
        foreach (var messageType in messageKeyMap.Keys)
        {
            var topicName = ResolveTopicName(messageType);
            var topic = await sqsConnection.Sns.CreateTopicAsync(topicName, cancellationToken);
            _topicArnsByMessageType[messageType] = topic.TopicArn;
        }

        // Invert messageType -> consumerTypes[] into consumerType -> messageTypes it consumes,
        // so each queue only subscribes to the topics it actually needs.
        var messageTypesByConsumerType = messageKeyMap
            .SelectMany(kv => kv.Value.Select(consumerType => (consumerType, messageType: kv.Key)))
            .GroupBy(x => x.consumerType, x => x.messageType);

        foreach (var group in messageTypesByConsumerType)
        {
            var consumerType = group.Key;
            var queueName = ResolveQueueName(consumerType);
            var queue = await sqsConnection.Sqs.CreateQueueAsync(new CreateQueueRequest { QueueName = queueName },
                cancellationToken);
            var queueUrl = queue.QueueUrl;
            _queueUrlsByConsumerType[consumerType] = queueUrl;

            var queueArn = await GetQueueArnAsync(queueUrl, cancellationToken);
            var topicArns = group.Select(messageType => _topicArnsByMessageType[messageType]).ToArray();

            await AllowTopicsToPublishToQueueAsync(queueUrl, queueArn, topicArns, cancellationToken);
            foreach (var topicArn in topicArns)
                await SubscribeQueueToTopicAsync(topicArn, queueArn, cancellationToken);

            // Fire-and-forget, same shape as RabbitMqClient's MonitorRecycleAsync loops — runs
            // until cancellationToken (the token StartAsync itself was given) is cancelled.
            _ = PollLoopAsync(queueUrl, consumerType, cancellationToken);
        }

        // Run until StopAsync/host shutdown cancels this token — same contract as
        // RabbitMqClient.StartAsync (see IMessageBrokerConnector.StartAsync's doc comment).
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    // A message type can override its SNS topic name via IMessageDefinition<TMessage>'s
    // MessageConfigurator.Name(...) — same mechanism RabbitMqClient.GetExchangeName uses. Unlike
    // that method, an IMessageDefinition<TMessage> registered without a name explicitly set (e.g.
    // one that only calls UseRawJsonSerializer()) falls back to the type-based default here
    // instead of resolving to a blank/null topic name.
    private string ResolveTopicName(Type messageType)
    {
        var messageDefinition = serviceProvider.GetService(typeof(IMessageDefinition<>)
            .MakeGenericType(messageType)) as IMessageDefinition;
        var customName = messageDefinition?.MessageConfigurator.GetName();
        return customName is { Length: > 0 } ? SqsNamingExtensions.Sanitize(customName) : messageType.GetTopicName();
    }

    // A consumer can override its SQS queue name via IConsumerConfigurator<TConsumer>.ReceivedEndpoint(...)
    // (FxLink.Aws.Sqs.Extensions.ConsumerDispatchExtensions) — same mechanism/shape as RabbitMq's
    // own ReceivedEndpoint, just without AutoDelete (no SQS equivalent).
    private string ResolveQueueName(Type consumerType)
    {
        var consumerConfiguration = serviceProvider
                .GetRequiredService(typeof(IConsumerConfiguratorResolver<>).MakeGenericType(consumerType)) as
            IConsumerConfiguratorResolver;
        var customName = consumerConfiguration!.Resolve<ISqsReceiveEndpointDefinition>()?.ReceiveEndpoint;
        return customName is { Length: > 0 } ? SqsNamingExtensions.Sanitize(customName) : consumerType.GetQueueName();
    }

    private async Task<string> GetQueueArnAsync(string queueUrl, CancellationToken token)
    {
        var response = await sqsConnection.Sqs.GetQueueAttributesAsync(queueUrl,
            [QueueAttributeName.QueueArn], token);
        return response.QueueARN;
    }

    // SNS won't be able to deliver to the queue without this — a subscription can be "confirmed"
    // while every publish still silently fails, because SendMessage is denied by the queue's own
    // access policy (separate from any IAM role policy) until SNS is explicitly allowed in.
    private async Task AllowTopicsToPublishToQueueAsync(string queueUrl, string queueArn, string[] topicArns,
        CancellationToken token)
    {
        var policy = new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Effect = "Allow",
                    Principal = new { Service = "sns.amazonaws.com" },
                    Action = "sqs:SendMessage",
                    Resource = queueArn,
                    Condition = new Dictionary<string, object>
                    {
                        ["ArnEquals"] = new Dictionary<string, object> { ["aws:SourceArn"] = topicArns }
                    }
                }
            }
        };

        await sqsConnection.Sqs.SetQueueAttributesAsync(queueUrl,
            new Dictionary<string, string> { [QueueAttributeName.Policy] = JsonSerializer.Serialize(policy) },
            token);
    }

    private async Task SubscribeQueueToTopicAsync(string topicArn, string queueArn, CancellationToken token)
    {
        // Idempotent: SNS returns the existing SubscriptionArn if this (topic, protocol, endpoint)
        // combination already exists, so it's safe to call on every StartAsync.
        var subscribeResponse = await sqsConnection.Sns.SubscribeAsync(new SubscribeRequest
        {
            TopicArn = topicArn,
            Protocol = "sqs",
            Endpoint = queueArn
        }, token);

        // Without this, SQS receives the message wrapped in an SNS envelope (Type/MessageId/
        // TopicArn/Message/...) instead of the raw payload FxLink published — the consume path
        // would have to unwrap an extra JSON layer that doesn't exist on the RabbitMq transport.
        await sqsConnection.Sns.SetSubscriptionAttributesAsync(subscribeResponse.SubscriptionArn,
            "RawMessageDelivery", "true", token);
    }

    // SQS has no push-based consume like AMQP's BasicConsumeAsync — this long-polls the queue
    // in a loop instead. One loop per consumer type/queue, all started from StartAsync.
    private async Task PollLoopAsync(string queueUrl, Type consumerType, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            ReceiveMessageResponse response;
            try
            {
                response = await sqsConnection.Sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20, // long-poll — avoids hammering SQS with empty receives
                    MessageAttributeNames = ["All"] // must opt in, or MessageAttributes comes back empty
                }, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // A transient AWS API failure must not silently kill this fire-and-forget loop
                // forever — log and retry after a short delay instead.
                _logger.LogWarning(ex, "ReceiveMessage failed for queue {QueueUrl}; retrying", queueUrl);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                continue;
            }

            if (response.Messages.Count == 0) continue;

            await Task.WhenAll(response.Messages.Select(message =>
                ProcessAndDeleteAsync(queueUrl, message, consumerType, token)));
        }
    }

    private async Task ProcessAndDeleteAsync(string queueUrl, Message message, Type consumerType,
        CancellationToken token)
    {
        try
        {
            if (message.MessageAttributes.TryGetValue("MessageType", out var attribute) &&
                attribute.StringValue is { Length: > 0 } messageTypeName)
            {
                var connectorType = _connectorTypeCache.GetOrAdd(messageTypeName, static type =>
                {
                    var msgType = Type.GetType(type);
                    return msgType is null ? null : typeof(IClientConnector<>).MakeGenericType(msgType);
                });

                if (connectorType is not null)
                {
                    var connector = (AbstractSqsConnector)serviceProvider.GetRequiredService(connectorType);
                    await connector.ProcessMessageReceivedAsync(message, consumerType, token);
                }
            }
        }
        catch (Exception ex)
        {
            // RetryPipelineBehavior already owns retry/dead-letter handling and never rethrows in
            // the normal case — reaching here means that mechanism itself failed. Delete anyway to
            // avoid an uncontrolled redelivery loop; the failure is only visible via this log.
            _logger.LogCritical(ex, "Unhandled exception escaped the consumer pipeline for message {MessageId}",
                message.MessageId);
        }
        finally
        {
            // Deliberately CancellationToken.None: a message that was already successfully
            // processed must still be deleted even if the caller's token just got cancelled —
            // losing the delete here would mean reprocessing it from scratch on restart.
            await sqsConnection.Sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, CancellationToken.None);
        }
    }

    public string GetTopicArn(Type messageType)
    {
        if (_topicArnsByMessageType.TryGetValue(messageType, out var topicArn)) return topicArn;
        throw new InvalidOperationException(
            $"No SNS topic registered for message type {messageType.FullName}. StartAsync must run " +
            "(and the message type must be registered on this transport) before it can be published.");
    }

    public Task PublishAsync(string topicArn, string messageBody, string messageTypeName,
        CancellationToken token = default) => sqsConnection.Sns.PublishAsync(new PublishRequest
    {
        TopicArn = topicArn,
        Message = messageBody,
        MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
        {
            // Read back on receive to resolve which IClientConnector<TMessage>/IConsumerConnector<TMessage>
            // to dispatch into — survives SNS->SQS delivery because RawMessageDelivery is enabled
            // on every subscription (see SubscribeQueueToTopicAsync).
            ["MessageType"] = new() { DataType = "String", StringValue = messageTypeName }
        }
    }, token);

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        // Nothing to actively tear down yet: the SNS/SQS clients are plain HTTP clients owned
        // (and disposed) by the ISqsConnection singleton, not a stateful connection SqsClient
        // itself holds the way RabbitMqClient.StopAsync closes an actual AMQP connection. Once
        // poll loops exist, this is where they'll be signalled to stop and awaited.
        return Task.CompletedTask;
    }
}
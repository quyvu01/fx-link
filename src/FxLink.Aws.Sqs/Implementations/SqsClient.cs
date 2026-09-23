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
    IServiceProvider serviceProvider,
    ISqsConfiguration sqsConfiguration)
    : IMessageBrokerConnector, ISqsClient
{
    private readonly ILogger<SqsClient> _logger = serviceProvider.GetRequiredService<ILogger<SqsClient>>();

    // Populated by StartAsync, read by PublishAsync/the poll loop below — one SNS topic per
    // message type is the fan-out point (mirrors RabbitMq's fanout exchange), one SQS queue per
    // consumer type is what each poll loop reads from.
    private readonly Dictionary<Type, string> _topicArnsByMessageType = new();
    private readonly Dictionary<Type, string> _queueUrlsByConsumerType = new();

    // Every queue subscribed to a given message type's topic — the fan-out set a delayed publish
    // (see SqsDelayMessageProvider) has to reconstruct by hand, since it can't go through SNS.
    private readonly Dictionary<Type, List<string>> _queueUrlsByMessageType = new();

    // messageTypeName (AssemblyQualifiedName, from the "MessageType" attribute) -> the closed
    // IClientConnector<> type to resolve from DI — same caching role as RabbitMqClient's
    // _connectorTypeCache.
    private readonly ConcurrentDictionary<string, Type> _connectorTypeCache = new();

    // Unique per process instance (see StartAsync) — unlike a consumer queue, nothing else should
    // ever read from or know about this one, so it's created fresh on every start and deleted in
    // StopAsync rather than being a stable, well-known name.
    private string _replyQueueUrl;
    public string ReplyQueueUrl => _replyQueueUrl ?? throw new InvalidOperationException(
        "The reply queue isn't ready yet — StartAsync must run before a request can be sent.");

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
            var messageTypes = group.ToArray();
            var topicArns = messageTypes.Select(messageType => _topicArnsByMessageType[messageType]).ToArray();

            foreach (var messageType in messageTypes)
            {
                if (!_queueUrlsByMessageType.TryGetValue(messageType, out var queueUrls))
                    _queueUrlsByMessageType[messageType] = queueUrls = [];
                queueUrls.Add(queueUrl);
            }

            await AllowTopicsToPublishToQueueAsync(queueUrl, queueArn, topicArns, cancellationToken);
            foreach (var topicArn in topicArns)
                await SubscribeQueueToTopicAsync(topicArn, queueArn, cancellationToken);

            await ConfigureDeadLetterQueueAsync(queueName, queueUrl, cancellationToken);

            // Fire-and-forget, same shape as RabbitMqClient's MonitorRecycleAsync loops — runs
            // until cancellationToken (the token StartAsync itself was given) is cancelled.
            _ = PollLoopAsync(queueUrl, consumerType, cancellationToken);
        }

        // Per-instance reply queue for request/response — unique name so replies can never be
        // misdelivered to a different instance's poller (see ISqsClient.ReplyQueueUrl).
        var replyQueueName = SqsNamingExtensions.Sanitize($"reply-{Guid.NewGuid():N}");
        var replyQueue = await sqsConnection.Sqs.CreateQueueAsync(
            new CreateQueueRequest { QueueName = replyQueueName }, cancellationToken);
        _replyQueueUrl = replyQueue.QueueUrl;
        _ = ReplyPollLoopAsync(_replyQueueUrl, cancellationToken);

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

    // Creates a per-queue dead-letter queue and points the main queue's RedrivePolicy at it —
    // native SQS mechanism, unlike RabbitMq's hand-rolled DLX+queue-bind dance. Purely a
    // broker-level safety net (see ISqsConfigurator.MaxReceiveCount's doc comment); it does NOT
    // need an SNS-style access policy, since SQS moves messages queue-to-queue internally rather
    // than through a publish.
    private async Task ConfigureDeadLetterQueueAsync(string queueName, string queueUrl, CancellationToken token)
    {
        var deadLetterQueueName = queueName.DeadLetterQueueName();
        var deadLetterQueue = await sqsConnection.Sqs.CreateQueueAsync(
            new CreateQueueRequest { QueueName = deadLetterQueueName }, token);
        var deadLetterQueueArn = await GetQueueArnAsync(deadLetterQueue.QueueUrl, token);

        var redrivePolicy = new
        {
            deadLetterTargetArn = deadLetterQueueArn,
            maxReceiveCount = sqsConfiguration.MaxReceiveCount
        };

        await sqsConnection.Sqs.SetQueueAttributesAsync(queueUrl,
            new Dictionary<string, string> { [QueueAttributeName.RedrivePolicy] = JsonSerializer.Serialize(redrivePolicy) },
            token);
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

    // Same long-poll shape as PollLoopAsync, but for this instance's own reply queue — every
    // message on it is a response to one of THIS instance's own outstanding requests, dispatched
    // via ProcessResponseMessageAsync instead of ProcessMessageReceivedAsync (no consumerType,
    // no IConsumerConnector<TMessage> — it completes an in-process await instead).
    private async Task ReplyPollLoopAsync(string queueUrl, CancellationToken token)
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
                    WaitTimeSeconds = 20,
                    MessageAttributeNames = ["All"]
                }, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ReceiveMessage failed for reply queue {QueueUrl}; retrying", queueUrl);
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

            await Task.WhenAll(response.Messages.Select(message => ProcessAndDeleteResponseAsync(queueUrl, message, token)));
        }
    }

    private async Task ProcessAndDeleteResponseAsync(string queueUrl, Message message, CancellationToken token)
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
                    await connector.ProcessResponseMessageAsync(message, token);
                }
            }
        }
        catch (Exception ex)
        {
            // Mirrors ProcessAndDeleteAsync's reasoning: whoever was waiting on this response has
            // its own timeout (Requester<TRequest>'s own CancelAfter), so there's nothing useful
            // to retry here — just make the failure visible and move on.
            _logger.LogCritical(ex, "Unhandled exception processing a response message {MessageId}",
                message.MessageId);
        }
        finally
        {
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

    public IReadOnlyList<string> GetQueueUrlsForMessageType(Type messageType) =>
        _queueUrlsByMessageType.GetValueOrDefault(messageType, []);

    public Task PublishAsync(string topicArn, string messageBody, string messageTypeName,
        string replyToQueueUrl = null, CancellationToken token = default)
    {
        var attributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
        {
            // Read back on receive to resolve which IClientConnector<TMessage>/IConsumerConnector<TMessage>
            // to dispatch into — survives SNS->SQS delivery because RawMessageDelivery is enabled
            // on every subscription (see SubscribeQueueToTopicAsync).
            ["MessageType"] = new() { DataType = "String", StringValue = messageTypeName }
        };
        if (replyToQueueUrl is { Length: > 0 })
            attributes["ReplyTo"] = new Amazon.SimpleNotificationService.Model.MessageAttributeValue
                { DataType = "String", StringValue = replyToQueueUrl };

        return sqsConnection.Sns.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = messageBody,
            MessageAttributes = attributes
        }, token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // The regular per-consumer queues are stable, well-known names meant to outlive this
        // instance (other instances of the same consumer type still read from them) — only the
        // reply queue is unique to this instance and has no reason to persist after it exits.
        if (_replyQueueUrl is null) return;
        try
        {
            // CancellationToken.None deliberately — a shutdown-cancelled token must not abort the
            // delete itself, or the queue leaks in the AWS account for good.
            await sqsConnection.Sqs.DeleteQueueAsync(_replyQueueUrl, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete reply queue {QueueUrl} on shutdown", _replyQueueUrl);
        }
    }
}
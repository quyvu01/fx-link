namespace FxLink.Azure.ServiceBus.Abstractions;

// What ServiceBusClientConnector<TMessage> needs from the transport client — mirrors INatsMessagingClient's role.
internal interface IServiceBusMessagingClient
{
    // Deterministic from the message type, so this works for a type this process only publishes.
    string GetTopicName(Type messageType);

    Task PublishAsync(string topic, string messageBody, string messageTypeName, OutgoingOptions options = default,
        CancellationToken token = default);

    // The response leg: one message into the shared reply queue, addressed to the requester's session.
    Task PublishReplyAsync(string replyQueue, string sessionId, string messageBody, string messageTypeName,
        CancellationToken token = default);

    // Where this instance's own requests ask for the reply: the shared queue and this instance's session.
    string ReplyQueueName { get; }
    string ReplySessionId { get; }
}

internal readonly record struct OutgoingOptions(
    // Set for a plain publish so the outbox re-sending a message doesn't create a duplicate (when the topic
    // detects duplicates); left unset for retries, which reuse the original MessageId.
    Guid? MessageId = null,
    bool IsRequest = false,
    DateTimeOffset? ScheduledEnqueueTime = null,
    // Restricts delivery to one subscription (see ServiceBusTopology.TargetRule) — used by retries.
    string TargetSubscription = null);

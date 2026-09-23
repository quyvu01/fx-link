namespace FxLink.Aws.Sqs.Abstractions;

// What SqsClientConnector<TMessage> needs from SqsClient — mirrors IRabbitMqClient's role
// (publish + topology lookups), kept as a thin interface so the connector depends on the
// contract, not the IMessageBrokerConnector implementation itself.
internal interface ISqsClient
{
    // Throws if messageType has no topic — StartAsync creates one for every message type in
    // IMessageKeys.GetMessageKeys(), so this only fires if a message is published before
    // StartAsync has run, or for a type that was never registered for this transport.
    string GetTopicArn(Type messageType);

    // replyToQueueUrl is set for the request leg of a request/response call — carried as a wire
    // attribute so the responding side knows where to send its reply back to (see
    // SqsClientConnector<TMessage>.ProcessMessageReceivedAsync's ReplyTo handling).
    Task PublishAsync(string topicArn, string messageBody, string messageTypeName, string replyToQueueUrl = null,
        CancellationToken token = default);

    // SNS PublishRequest has no per-message delay — only SQS's own SendMessageRequest.DelaySeconds
    // does. A delayed publish has to bypass the topic and go directly to every queue that would
    // otherwise have received it via that topic's subscription — this is what a delay provider
    // needs to reconstruct that fan-out by hand. Same registration-required caveat as GetTopicArn.
    IReadOnlyList<string> GetQueueUrlsForMessageType(Type messageType);

    // The per-instance reply queue a request should set as its "ReplyTo" wire attribute — mirrors
    // IRabbitMqClient.ReplyQueueName, except it's a full queue URL (SQS has no "route by name via
    // the default exchange" equivalent, so the responder needs the actual address to send to) and
    // is unique per process instance (see SqsClient.StartAsync), not shared like a consumer queue.
    string ReplyQueueUrl { get; }
}

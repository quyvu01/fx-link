namespace FxLink.Nats.Abstractions;

// What NatsClientConnector<TMessage> needs from NatsClient — mirrors IRabbitMqClient/ISqsClient's
// role, kept thin so the connector depends on the contract, not the IMessageBrokerConnector.
internal interface INatsMessagingClient
{
    // Deterministic from the message type (no server-side lookup like an SNS topic ARN), so unlike
    // SQS this works for a message type this process only publishes and never consumes.
    string GetSubject(Type messageType);

    // Where a message that exhausted its retries (or hit an ignored exception) is parked. Each
    // consumer has a durable "{consumer}-deadletter" consumer filtering on it, so the message is
    // retained for inspection instead of being dropped for lack of interest.
    string GetDeadLetterSubject(Type messageType);

    // Published through JetStream, so it completes only once the stream has persisted the message
    // (or fails) — Core NATS would fire and forget. messageId doubles as the JetStream dedup id, so
    // a retried publish of the same message (e.g. from the outbox dispatcher) within the stream's
    // duplicate window isn't stored twice. replyTo is set on the request leg of request/response.
    Task PublishAsync(string subject, string messageBody, string messageTypeName, Guid? messageId = null,
        string replyTo = null, CancellationToken token = default);

    // Delayed delivery (retry backoff, delayed publish) via NATS message scheduling: the message is
    // stored under a unique holding subject and the server republishes it to targetSubject once
    // the delay elapses. No dedup id here on purpose — a retry re-sends the same MessageId as the
    // original delivery, which JetStream would otherwise silently drop as a duplicate.
    Task PublishScheduledAsync(string targetSubject, string messageBody, string messageTypeName, TimeSpan delay,
        CancellationToken token = default);

    // The response leg: plain Core NATS publish straight to the requester's reply subject. Not
    // persisted — like an AMQP reply queue, a response nobody is waiting for any more is just lost.
    Task PublishReplyAsync(string replySubject, string messageBody, string messageTypeName,
        CancellationToken token = default);

    // This instance's own inbox subject a request should carry as its "ReplyTo".
    string ReplySubject { get; }
}

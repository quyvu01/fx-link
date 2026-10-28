namespace FxLink.Nats.Registries;

public interface INatsConfigurator
{
    /// <summary>
    /// NATS server URL(s), e.g. "nats://localhost:4222". Several servers can be given
    /// comma-separated; the client fails over between them.
    /// </summary>
    void Server(string url, Action<NatsCredential> configure = null);

    /// <summary>
    /// JetStream stream every FxLink subject lives in. Defaults to "FXLINK".
    /// </summary>
    void StreamName(string streamName);

    /// <summary>
    /// First token of every subject FxLink uses ("{prefix}.{message}"); the stream captures
    /// "{prefix}.>". Defaults to "fxlink".
    /// </summary>
    void SubjectPrefix(string subjectPrefix);

    /// <summary>
    /// Whether StartAsync creates/updates the stream and consumers itself (idempotent). Turn off
    /// when they are provisioned externally; FxLink then only binds to existing ones. Defaults to true.
    /// </summary>
    void AutoProvision(bool autoProvision);

    /// <summary>
    /// Broker-level redelivery cap per message before JetStream stops redelivering it — a safety
    /// net for consumers that crash mid-handling, distinct from RetryPipelineBehavior's in-process
    /// retry. Defaults to 5.
    /// </summary>
    void MaxDeliver(int maxDeliver);

    /// <summary>
    /// How long JetStream waits for an ack before redelivering (the analogue of SQS's visibility
    /// timeout). Defaults to 30 seconds.
    /// </summary>
    void AckWait(TimeSpan ackWait);

    /// <summary>
    /// Max unacknowledged messages in flight per consumer (the analogue of RabbitMq's prefetch
    /// count). Defaults to 100.
    /// </summary>
    void MaxAckPending(int maxAckPending);

    /// <summary>
    /// How long the stream keeps messages, acked or not (dead letters included, since nothing
    /// consumes them). The stream uses limits retention rather than interest retention because
    /// message scheduling (delay/retry) needs the scheduled message to be stored even though no
    /// consumer is interested in its holding subject. Defaults to 7 days.
    /// </summary>
    void MaxAge(TimeSpan maxAge);

    /// <summary>
    /// Controls what happens to a message when an exception escapes the consumer pipeline — i.e. the
    /// retry/dead-letter mechanism itself failed (republish failed, Inbox store unreachable, ...), not a
    /// consumer exception, which <c>RetryPipelineBehavior</c> already handles. By default such a message
    /// is acked and only logged, so it is lost. When configured, it is instead negatively acknowledged
    /// so JetStream redelivers it after <paramref name="delay"/>, once the dependency recovers. Each
    /// redelivery counts towards <see cref="MaxDeliver"/>, after which JetStream stops redelivering it.
    /// </summary>
    void RequeueOnFailure(TimeSpan delay);
}

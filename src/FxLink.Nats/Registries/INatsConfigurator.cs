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
}

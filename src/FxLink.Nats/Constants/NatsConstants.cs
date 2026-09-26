namespace FxLink.Nats.Constants;

internal static class NatsConstants
{
    public const string DefaultStreamName = "FXLINK";
    public const string DefaultSubjectPrefix = "fxlink";
    public const int DefaultMaxDeliver = 5;
    public const int DefaultMaxAckPending = 100;
    public static readonly TimeSpan DefaultAckWait = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(7);

    // A consume/reply loop that fails this many times in a row (spaced ConsumeRetryDelay apart) is
    // treated as broken beyond self-recovery — typically the server lost its streams/consumers —
    // and faults the connector so the supervisor restarts it and re-provisions the topology.
    public const int ConsumeFailureThreshold = 3;
    public static readonly TimeSpan ConsumeRetryDelay = TimeSpan.FromSeconds(5);

    // Message scheduling (delay/retry) arrived with NATS server 2.12.
    public static readonly Version MinSchedulingServerVersion = new(2, 12);
}

using Amazon;

namespace FxLink.Aws.Sqs.Registries;

public interface ISqsConfigurator
{
    void Region(RegionEndpoint region, Action<ISqsCredential> configure = null);

    // Broker-level dead-letter safety net: a message becomes visible again on the same queue up
    // to this many times (ApproximateReceiveCount) before SQS moves it to that queue's
    // "{queue}-deadletter" queue via RedrivePolicy. This is a distinct, coarser mechanism from
    // RetryPipelineBehavior's in-process retry — it only fires when the consuming process itself
    // fails to even finish handling a delivery (crash, hang) before the visibility timeout
    // expires, not on an ordinary handler exception (which RetryPipelineBehavior already owns and
    // which still deletes the message afterward either way). Defaults to 5.
    void MaxReceiveCount(int maxReceiveCount);

    /// <summary>
    /// Controls what happens to a message when an exception escapes the consumer pipeline — i.e. the
    /// retry/dead-letter mechanism itself failed (republish failed, Inbox store unreachable, ...), not a
    /// consumer exception, which <c>RetryPipelineBehavior</c> already handles. By default such a message
    /// is deleted and only logged, so it is lost. When configured, it is instead left on the queue and
    /// made visible again after <paramref name="delay"/> (rounded up to whole seconds, at most 12 hours),
    /// so it is redelivered once the dependency recovers. Each redelivery counts towards
    /// <see cref="MaxReceiveCount"/>, after which SQS moves it to the dead-letter queue.
    /// </summary>
    void RequeueOnFailure(TimeSpan delay);
}

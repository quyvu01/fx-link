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
}

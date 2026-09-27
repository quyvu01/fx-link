using Azure.Core;

namespace FxLink.Azure.ServiceBus.Registries;

public interface IServiceBusConfigurator
{
    /// <summary>
    /// Connects with a connection string (shared access key). Use
    /// "UseDevelopmentEmulator=true" in it to target the Service Bus emulator.
    /// </summary>
    void ConnectionString(string connectionString);

    /// <summary>
    /// Connects to "{name}.servicebus.windows.net" with Microsoft Entra ID. Uses
    /// DefaultAzureCredential unless a credential is supplied.
    /// </summary>
    void Namespace(string fullyQualifiedNamespace, TokenCredential credential = null);

    /// <summary>
    /// Whether StartAsync creates/updates topics, subscriptions, rules and the reply queue itself
    /// (idempotent). Turn off when they are provisioned externally or the server can't manage
    /// entities (the emulator) — see IServiceBusTopologyExporter for the expected layout. Defaults to true.
    /// </summary>
    void AutoProvision(bool autoProvision);

    /// <summary>
    /// The session-enabled queue request/response replies travel through, shared by every
    /// instance. Defaults to "fxlink-replies".
    /// </summary>
    void ReplyQueueName(string replyQueueName);

    /// <summary>
    /// Deliveries a message gets before Service Bus moves it to the subscription's dead-letter
    /// queue — a broker-level safety net distinct from RetryPipelineBehavior's retry. Defaults to 5.
    /// </summary>
    void MaxDeliveryCount(int maxDeliveryCount);

    /// <summary>
    /// How long a received message stays locked to this consumer (5 seconds to 5 minutes).
    /// The SDK renews it while a handler runs. Defaults to 30 seconds.
    /// </summary>
    void LockDuration(TimeSpan lockDuration);

    /// <summary>
    /// Messages handled concurrently per subscription. Defaults to 8.
    /// </summary>
    void MaxConcurrentCalls(int maxConcurrentCalls);

    /// <summary>
    /// Messages fetched ahead of processing per subscription (0 disables prefetch, the default).
    /// </summary>
    void PrefetchCount(int prefetchCount);

    /// <summary>
    /// Longest a handler can run before its lock stops being renewed and the message is
    /// redelivered. Defaults to 5 minutes.
    /// </summary>
    void MaxAutoLockRenewalDuration(TimeSpan maxAutoLockRenewalDuration);
}

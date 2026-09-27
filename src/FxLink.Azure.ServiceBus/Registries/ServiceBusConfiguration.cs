using Azure.Core;

namespace FxLink.Azure.ServiceBus.Registries;

internal sealed class ServiceBusConfiguration(
    string connectionString,
    string fullyQualifiedNamespace,
    TokenCredential credential,
    bool autoProvision,
    string replyQueueName,
    int maxDeliveryCount,
    TimeSpan lockDuration,
    int maxConcurrentCalls,
    int prefetchCount,
    TimeSpan maxAutoLockRenewalDuration)
    : IServiceBusConfiguration
{
    public string ConnectionString { get; } = connectionString;
    public string FullyQualifiedNamespace { get; } = fullyQualifiedNamespace;
    public TokenCredential Credential { get; } = credential;
    public bool AutoProvision { get; } = autoProvision;
    public string ReplyQueueName { get; } = replyQueueName;
    public int MaxDeliveryCount { get; } = maxDeliveryCount;
    public TimeSpan LockDuration { get; } = lockDuration;
    public int MaxConcurrentCalls { get; } = maxConcurrentCalls;
    public int PrefetchCount { get; } = prefetchCount;
    public TimeSpan MaxAutoLockRenewalDuration { get; } = maxAutoLockRenewalDuration;
}

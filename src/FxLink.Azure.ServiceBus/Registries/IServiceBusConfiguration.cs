using Azure.Core;

namespace FxLink.Azure.ServiceBus.Registries;

internal interface IServiceBusConfiguration
{
    string ConnectionString { get; }
    string FullyQualifiedNamespace { get; }
    TokenCredential Credential { get; }
    bool AutoProvision { get; }
    string ReplyQueueName { get; }
    int MaxDeliveryCount { get; }
    TimeSpan LockDuration { get; }
    int MaxConcurrentCalls { get; }
    int PrefetchCount { get; }
    TimeSpan MaxAutoLockRenewalDuration { get; }
}

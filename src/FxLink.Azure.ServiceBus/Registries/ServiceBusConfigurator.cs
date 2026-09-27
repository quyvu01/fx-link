using Azure.Core;
using FxLink.Azure.ServiceBus.Constants;

namespace FxLink.Azure.ServiceBus.Registries;

internal sealed class ServiceBusConfigurator : IServiceBusConfigurator
{
    private string _connectionString;
    private string _fullyQualifiedNamespace;
    private TokenCredential _credential;
    private bool _autoProvision = true;
    private string _replyQueueName = ServiceBusConstants.DefaultReplyQueueName;
    private int _maxDeliveryCount = ServiceBusConstants.DefaultMaxDeliveryCount;
    private TimeSpan _lockDuration = ServiceBusConstants.DefaultLockDuration;
    private int _maxConcurrentCalls = ServiceBusConstants.DefaultMaxConcurrentCalls;
    private int _prefetchCount = ServiceBusConstants.DefaultPrefetchCount;
    private TimeSpan _maxAutoLockRenewalDuration = ServiceBusConstants.DefaultMaxAutoLockRenewalDuration;

    public void ConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public void Namespace(string fullyQualifiedNamespace, TokenCredential credential = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedNamespace);
        _fullyQualifiedNamespace = fullyQualifiedNamespace;
        _credential = credential;
    }

    public void AutoProvision(bool autoProvision) => _autoProvision = autoProvision;

    public void ReplyQueueName(string replyQueueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyQueueName);
        _replyQueueName = replyQueueName;
    }

    public void MaxDeliveryCount(int maxDeliveryCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDeliveryCount, 1);
        _maxDeliveryCount = maxDeliveryCount;
    }

    public void LockDuration(TimeSpan lockDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockDuration, ServiceBusConstants.MinLockDuration);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockDuration, ServiceBusConstants.MaxLockDuration);
        _lockDuration = lockDuration;
    }

    public void MaxConcurrentCalls(int maxConcurrentCalls)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentCalls, 1);
        _maxConcurrentCalls = maxConcurrentCalls;
    }

    public void PrefetchCount(int prefetchCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(prefetchCount, 0);
        _prefetchCount = prefetchCount;
    }

    public void MaxAutoLockRenewalDuration(TimeSpan maxAutoLockRenewalDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAutoLockRenewalDuration, TimeSpan.Zero);
        _maxAutoLockRenewalDuration = maxAutoLockRenewalDuration;
    }

    internal IServiceBusConfiguration ToConfiguration()
    {
        if (_connectionString is null == (_fullyQualifiedNamespace is null))
            throw new InvalidOperationException(
                "Azure Service Bus must be configured with exactly one of ConnectionString(...) or Namespace(...).");
        return new ServiceBusConfiguration(_connectionString, _fullyQualifiedNamespace, _credential, _autoProvision,
            _replyQueueName, _maxDeliveryCount, _lockDuration, _maxConcurrentCalls, _prefetchCount,
            _maxAutoLockRenewalDuration);
    }
}

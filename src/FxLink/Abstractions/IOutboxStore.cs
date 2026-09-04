using FxLink.Entities;

namespace FxLink.Abstractions;

public interface IOutboxStore
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken token = default);

    Task<IReadOnlyList<Guid>> GetPendingPartitionKeysAsync(int max, CancellationToken token = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingByPartitionAsync(Guid partitionKey, int max,
        CancellationToken token = default);

    Task<bool> MarkDispatchedAsync(Guid outboxMessageId, long leaseVersion, CancellationToken token = default);

    Task<bool> MarkFailedAsync(Guid outboxMessageId, long leaseVersion, string error,
        CancellationToken token = default);
    
    Task<bool> MarkDeadLetteredAsync(Guid outboxMessageId, long leaseVersion, string reason,
        CancellationToken token = default);

    Task DeleteDispatchedBeforeAsync(DateTime cutoff, CancellationToken token = default);
}
using FxLink.Entities;

namespace FxLink.Abstractions;

public interface IOutboxStore
{
    // The caller's ambient unit of work owns commit — implementations must only track/stage this
    // write (e.g. DbSet.Add), never call SaveChanges/commit themselves, so the row lands atomically
    // with whatever business write the caller is making in the same scope.
    Task EnqueueAsync(OutboxMessage message, CancellationToken token = default);

    Task<IReadOnlyList<Guid>> GetPendingPartitionKeysAsync(int max, CancellationToken token = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingByPartitionAsync(Guid partitionKey, int max,
        CancellationToken token = default);

    // Removes the message from the store entirely on success — there is no retained "Dispatched"
    // row to observe afterward. Returns false (leaving the row untouched) when leaseVersion no
    // longer matches the current fencing token.
    Task<bool> MarkDispatchedAsync(Guid outboxMessageId, long leaseVersion, CancellationToken token = default);

    Task<bool> MarkFailedAsync(Guid outboxMessageId, long leaseVersion, string error,
        CancellationToken token = default);

    Task<bool> MarkDeadLetteredAsync(Guid outboxMessageId, long leaseVersion, string reason,
        CancellationToken token = default);

    // Only DeadLettered rows ever need sweeping — a Dispatched row never persists long enough to
    // need retention (see MarkDispatchedAsync).
    Task DeleteDispatchedBeforeAsync(DateTime cutoff, CancellationToken token = default);
}
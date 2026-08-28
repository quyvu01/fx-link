using FxLink.Entities;

namespace FxLink.Abstractions;

// Backing store for the Outbox pattern. EnqueueAsync must be called using the same unit-of-work
// (e.g. DbContext) the business write uses, and must NOT commit it — the caller owns the commit so
// the outbox row lands atomically with the business data it describes. Everything else here is used
// by the dispatcher, which runs in a separate scope/process and only ever sees already-committed rows.
//
// MarkDispatchedAsync/MarkFailedAsync/MarkDeadLetteredAsync take the caller's current
// IPartitionLeaseStore fencing token and return false instead of writing when it's stale — that's
// the dispatcher's signal that its lease was reclaimed by another instance and it must stop touching
// this partition immediately, even though the in-process code still believes it holds the lease.
public interface IOutboxStore
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken token = default);

    Task<IReadOnlyList<Guid>> GetPendingPartitionKeysAsync(int max, CancellationToken token = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingByPartitionAsync(Guid partitionKey, int max,
        CancellationToken token = default);

    Task<bool> MarkDispatchedAsync(Guid outboxMessageId, long leaseVersion, CancellationToken token = default);

    Task<bool> MarkFailedAsync(Guid outboxMessageId, long leaseVersion, string error,
        CancellationToken token = default);

    // Terminal state for a poisoned message: excluded from future GetPendingByPartitionAsync results
    // so the partition can proceed past it, without being reported as successfully Dispatched.
    Task<bool> MarkDeadLetteredAsync(Guid outboxMessageId, long leaseVersion, string reason,
        CancellationToken token = default);

    Task DeleteDispatchedBeforeAsync(DateTime cutoff, CancellationToken token = default);
}

namespace FxLink.Abstractions;

// Cross-instance coordination primitive so only one dispatcher instance drives a given PartitionKey
// at a time, while different PartitionKeys can be leased out to different instances in parallel.
// Leases expire on their own (LeasedUntil) so a crashed owner is reclaimed automatically instead of
// needing an explicit release; ReleaseAsync is only an optimization for the clean-shutdown path.
//
// The returned/passed version is a fencing token: it increments on every successful acquire or
// renew, and callers must pass the version they last obtained into IOutboxStore's Mark*Async calls.
// This protects against a paused/stalled owner writing after its lease was already reclaimed by
// someone else — the write is rejected by version, not by the (possibly stale) owner's own belief
// that it still holds the lease.
public interface IPartitionLeaseStore
{
    // Returns the new fencing token, or null if another owner currently holds a live lease.
    Task<long?> TryAcquireAsync(Guid partitionKey, string ownerId, TimeSpan leaseDuration,
        CancellationToken token = default);

    // Returns the renewed fencing token, or null if the lease was already reclaimed by another owner.
    Task<long?> RenewAsync(Guid partitionKey, string ownerId, long leaseVersion, TimeSpan leaseDuration,
        CancellationToken token = default);

    Task ReleaseAsync(Guid partitionKey, string ownerId, long leaseVersion, CancellationToken token = default);
}

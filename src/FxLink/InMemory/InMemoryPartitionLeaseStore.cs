using FxLink.Abstractions;

namespace FxLink.InMemory;

// Single-process reference implementation. Real cross-instance split-brain (the scenario fencing
// tokens defend against) cannot happen here — separate processes each get their own empty
// dictionary, they don't share leases at all. The version/fencing plumbing is still implemented
// faithfully so it can be exercised by tests that simulate concurrent dispatch within one process
// (e.g. two Task loops racing the same store), and so InMemoryOutboxStore has something real to
// check against.
internal sealed class InMemoryPartitionLeaseStore : IPartitionLeaseStore
{
    private sealed class LeaseRecord
    {
        public string OwnerId;
        public long Version;
        public DateTime LeasedUntil;
    }

    private readonly Dictionary<Guid, LeaseRecord> _leases = new();
    private readonly object _gate = new();

    public Task<long?> TryAcquireAsync(Guid partitionKey, string ownerId, TimeSpan leaseDuration,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if (_leases.TryGetValue(partitionKey, out var existing) &&
                existing.LeasedUntil > now && existing.OwnerId != ownerId)
                return Task.FromResult<long?>(null);

            var version = (existing?.Version ?? 0) + 1;
            _leases[partitionKey] = new LeaseRecord
            {
                OwnerId = ownerId, Version = version, LeasedUntil = now + leaseDuration
            };
            return Task.FromResult<long?>(version);
        }
    }

    public Task<long?> RenewAsync(Guid partitionKey, string ownerId, long leaseVersion, TimeSpan leaseDuration,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            if (!_leases.TryGetValue(partitionKey, out var existing) ||
                existing.OwnerId != ownerId || existing.Version != leaseVersion)
                return Task.FromResult<long?>(null);

            existing.Version++;
            existing.LeasedUntil = DateTime.UtcNow + leaseDuration;
            return Task.FromResult<long?>(existing.Version);
        }
    }

    public Task ReleaseAsync(Guid partitionKey, string ownerId, long leaseVersion, CancellationToken token = default)
    {
        lock (_gate)
        {
            // Expire in place rather than removing the record — removing would drop the Version
            // counter back to 0, letting a future TryAcquireAsync hand out a version number that
            // was already used before. A late in-flight write carrying the old (now-released)
            // version would then wrongly match the new owner's lease. Version must never be reused.
            if (_leases.TryGetValue(partitionKey, out var existing) &&
                existing.OwnerId == ownerId && existing.Version == leaseVersion)
                existing.LeasedUntil = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    // Not on IPartitionLeaseStore — a read-only peek so InMemoryOutboxStore can validate a fencing
    // token at Mark*Async time without mutating (renewing/stealing) the lease just to check it.
    internal bool IsCurrentVersion(Guid partitionKey, long version)
    {
        lock (_gate)
            return _leases.TryGetValue(partitionKey, out var existing) && existing.Version == version;
    }
}
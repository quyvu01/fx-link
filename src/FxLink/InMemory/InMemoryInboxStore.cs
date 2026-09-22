using FxLink.Abstractions;

namespace FxLink.InMemory;

// Single-process reference implementation — same caveat as InMemoryPartitionLeaseStore: real
// cross-instance split-brain can't happen here (separate processes each get their own empty
// dictionary), but the fencing/expiry plumbing is implemented faithfully so it can be exercised by
// tests that simulate concurrent redelivery within one process.
internal sealed class InMemoryInboxStore : IInboxStore
{
    private enum RecordState
    {
        Claimed,
        Processed
    }

    private sealed class Record
    {
        public RecordState State;
        public long Version;
        public DateTime ClaimedUntil;
        public DateTime? ProcessedAt;
    }

    private readonly Dictionary<(string ConsumerKey, Guid MessageId), Record> _records = new();
    private readonly object _gate = new();

    public Task<long?> TryClaimAsync(string consumerKey, Guid messageId, TimeSpan claimDuration,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            var key = (consumerKey, messageId);
            var now = DateTime.UtcNow;

            if (_records.TryGetValue(key, out var existing))
            {
                if (existing.State == RecordState.Processed) return Task.FromResult<long?>(null);
                if (existing.ClaimedUntil > now) return Task.FromResult<long?>(null); // still fresh, owned elsewhere

                existing.Version++; // steal: expired claim, bump the fencing token and extend it
                existing.ClaimedUntil = now + claimDuration;
                return Task.FromResult<long?>(existing.Version);
            }

            _records[key] = new Record
            {
                State = RecordState.Claimed, Version = 1, ClaimedUntil = now + claimDuration
            };
            return Task.FromResult<long?>(1L);
        }
    }

    public Task<long?> RenewClaimAsync(string consumerKey, Guid messageId, long claimVersion, TimeSpan claimDuration,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue((consumerKey, messageId), out var existing) ||
                existing.State != RecordState.Claimed || existing.Version != claimVersion)
                return Task.FromResult<long?>(null);

            existing.Version++;
            existing.ClaimedUntil = DateTime.UtcNow + claimDuration;
            return Task.FromResult<long?>(existing.Version);
        }
    }

    public Task<bool> MarkProcessedAsync(string consumerKey, Guid messageId, long claimVersion,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue((consumerKey, messageId), out var existing) ||
                existing.State != RecordState.Claimed || existing.Version != claimVersion)
                return Task.FromResult(false);

            existing.State = RecordState.Processed;
            existing.ProcessedAt = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    public Task ReleaseClaimAsync(string consumerKey, Guid messageId, long claimVersion,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            var key = (consumerKey, messageId);
            // Safe to remove outright (unlike InMemoryPartitionLeaseStore.ReleaseAsync, which expires
            // in place): a released claim was never Processed, so there's no history that a future
            // TryClaimAsync starting fresh at Version=1 could clobber or be confused with.
            if (_records.TryGetValue(key, out var existing) &&
                existing.State == RecordState.Claimed && existing.Version == claimVersion)
                _records.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task DeleteProcessedBeforeAsync(DateTime cutoff, CancellationToken token = default)
    {
        lock (_gate)
        {
            var expired = _records
                .Where(kv => kv.Value.State == RecordState.Processed &&
                             kv.Value.ProcessedAt is { } processedAt && processedAt < cutoff)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in expired) _records.Remove(key);
        }

        return Task.CompletedTask;
    }

    // Not on IInboxStore — test-only visibility into whether a record exists regardless of state.
    internal bool Contains(string consumerKey, Guid messageId)
    {
        lock (_gate)
            return _records.ContainsKey((consumerKey, messageId));
    }
}

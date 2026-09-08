using FxLink.Abstractions;
using FxLink.Entities;

namespace FxLink.InMemory;

// Paired with InMemoryPartitionLeaseStore — takes it as a concrete dependency (not IPartitionLeaseStore)
// so Mark*Async can validate a caller's fencing token via IsCurrentVersion without renewing/mutating
// the lease just to check it. Both must be registered as the same singleton instance (see
// OutboxConfigurator/MessageOutboxConfigurator) for fencing to mean anything.
internal sealed class InMemoryOutboxStore(InMemoryPartitionLeaseStore leaseStore) : IOutboxStore
{
    private readonly Dictionary<Guid, OutboxMessage> _messages = new();
    private readonly object _gate = new();
    private long _sequence;

    public Task EnqueueAsync(OutboxMessage message, CancellationToken token = default)
    {
        lock (_gate)
        {
            message.Sequence = ++_sequence;
            _messages[message.Id] = message;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetPendingPartitionKeysAsync(int max, CancellationToken token = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Guid> keys =
            [
                .. _messages.Values
                    .Where(IsPending)
                    .GroupBy(m => m.PartitionKey)
                    .Select(g => new { PartitionKey = g.Key, MinSequence = g.Min(m => m.Sequence) })
                    .OrderBy(x => x.MinSequence)
                    .Select(x => x.PartitionKey)
                    .Take(max)
            ];
            return Task.FromResult(keys);
        }
    }

    public Task<IReadOnlyList<OutboxMessage>> GetPendingByPartitionAsync(Guid partitionKey, int max,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            IReadOnlyList<OutboxMessage> messages =
            [
                .. _messages.Values
                    .Where(m => m.PartitionKey == partitionKey && IsPending(m))
                    .OrderBy(m => m.Sequence)
                    .Take(max)
            ];
            return Task.FromResult(messages);
        }
    }

    public Task<bool> MarkDispatchedAsync(Guid outboxMessageId, long leaseVersion, CancellationToken token = default)
    {
        lock (_gate)
        {
            if (!_messages.TryGetValue(outboxMessageId, out var message)) return Task.FromResult(false);
            if (!leaseStore.IsCurrentVersion(message.PartitionKey, leaseVersion)) return Task.FromResult(false);

            _messages.Remove(outboxMessageId);
            return Task.FromResult(true);
        }
    }

    public Task<bool> MarkFailedAsync(Guid outboxMessageId, long leaseVersion, string error,
        CancellationToken token = default) => TryMutate(outboxMessageId, leaseVersion, m =>
    {
        m.AttemptCount++;
        m.LastError = error;
    });

    public Task<bool> MarkDeadLetteredAsync(Guid outboxMessageId, long leaseVersion, string reason,
        CancellationToken token = default) => TryMutate(outboxMessageId, leaseVersion, m =>
    {
        m.DeadLetteredAt = DateTime.UtcNow;
        m.LastError = reason;
    });

    public Task DeleteDispatchedBeforeAsync(DateTime cutoff, CancellationToken token = default)
    {
        lock (_gate)
        {
            var expired = _messages.Values
                .Where(m => m.DeadLetteredAt is { } deadLetteredAt && deadLetteredAt < cutoff)
                .Select(m => m.Id)
                .ToList();

            foreach (var id in expired) _messages.Remove(id);
        }

        return Task.CompletedTask;
    }

    private static bool IsPending(OutboxMessage m) => m.DeadLetteredAt is null;

    // Not on IOutboxStore — test-only visibility into rows regardless of pending/terminal state,
    // since the public surface only ever exposes pending rows.
    internal bool Contains(Guid outboxMessageId)
    {
        lock (_gate)
        {
            return _messages.ContainsKey(outboxMessageId);
        }
    }

    private Task<bool> TryMutate(Guid outboxMessageId, long leaseVersion, Action<OutboxMessage> mutate)
    {
        lock (_gate)
        {
            if (!_messages.TryGetValue(outboxMessageId, out var message)) return Task.FromResult(false);
            if (!leaseStore.IsCurrentVersion(message.PartitionKey, leaseVersion)) return Task.FromResult(false);

            mutate(message);
            return Task.FromResult(true);
        }
    }
}
using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.Entities;
using FxLink.Outbox.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace FxLink.Outbox.EntityFrameworkCore.Repositories;

// SQL-backed IOutboxStore. dbContext is whatever the caller's scope resolved (see DbContextWrapped) —
// for EnqueueAsync specifically, that must be the SAME DbContext instance the business handler is
// using, so the row commits atomically with it; this class never calls SaveChangesAsync from
// EnqueueAsync for exactly that reason (see IOutboxStore's own doc comment).
internal sealed class EfOutboxStore(DbContext dbContext) : IOutboxStore
{
    private readonly DbSet<OutboxMessage> _outboxMessages = dbContext.Set<OutboxMessage>();

    public async Task EnqueueAsync(OutboxMessage message, CancellationToken token = default)
    {
        _outboxMessages.Add(message);
        await dbContext.SaveChangesAsync(token);
    }

    public async Task<IReadOnlyList<Guid>> GetPendingPartitionKeysAsync(int max, CancellationToken token = default) =>
        await _outboxMessages
            .Where(IsPending)
            .OrderBy(m => m.Sequence)
            .Select(m => m.PartitionKey)
            .Distinct()
            .Take(max)
            .ToListAsync(token);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingByPartitionAsync(Guid partitionKey, int max,
        CancellationToken token = default) =>
        await _outboxMessages
            .Where(m => m.PartitionKey == partitionKey)
            .Where(IsPending)
            .OrderBy(m => m.Sequence)
            .Take(max)
            .ToListAsync(token);

    public Task<bool> MarkDispatchedAsync(Guid outboxMessageId, long leaseVersion, CancellationToken token = default)
        => TryMutateAsync(outboxMessageId, leaseVersion, m => m.DispatchedAt = DateTime.UtcNow, token);

    public Task<bool> MarkFailedAsync(Guid outboxMessageId, long leaseVersion, string error,
        CancellationToken token = default) => TryMutateAsync(outboxMessageId, leaseVersion, m =>
    {
        m.AttemptCount++;
        m.LastError = error;
    }, token);

    public Task<bool> MarkDeadLetteredAsync(Guid outboxMessageId, long leaseVersion, string reason,
        CancellationToken token = default) => TryMutateAsync(outboxMessageId, leaseVersion, m =>
    {
        m.DeadLetteredAt = DateTime.UtcNow;
        m.LastError = reason;
    }, token);

    public async Task DeleteDispatchedBeforeAsync(DateTime cutoff, CancellationToken token = default)
    {
        var cutOffs = await _outboxMessages
            .Where(m => (m.DispatchedAt != null && m.DispatchedAt < cutoff) ||
                        (m.DeadLetteredAt != null && m.DeadLetteredAt < cutoff))
            .ToArrayAsync(token);
        _outboxMessages.RemoveRange(cutOffs);
        await dbContext.SaveChangesAsync(token);
    }

    private static Expression<Func<OutboxMessage, bool>> IsPending =>
        m => m.DispatchedAt == null && m.DeadLetteredAt == null;

    private async Task<bool> TryMutateAsync(Guid outboxMessageId, long leaseVersion, Action<OutboxMessage> mutate,
        CancellationToken token)
    {
        var message = await _outboxMessages.FindAsync([outboxMessageId], token);
        if (message is null) return false;

        var lease = await dbContext.Set<OutboxPartitionLease>().FindAsync([message.PartitionKey], token);
        if (lease is null || lease.Version != leaseVersion) return false;

        mutate.Invoke(message);
        dbContext.Entry(lease).Property(l => l.Version).IsModified = true;

        try
        {
            await dbContext.SaveChangesAsync(token);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
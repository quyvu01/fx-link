using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.Entities;
using FxLink.Outbox.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace FxLink.Outbox.EntityFrameworkCore.Repositories;

internal sealed class EfOutboxStore(DbContext dbContext) : IOutboxStore
{
    private readonly DbSet<OutboxMessage> _outboxMessages = dbContext.Set<OutboxMessage>();

    public Task EnqueueAsync(OutboxMessage message, CancellationToken token = default)
    {
        _outboxMessages.Add(message);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Guid>> GetPendingPartitionKeysAsync(int max, CancellationToken token = default) =>
        await _outboxMessages
            .Where(IsPending)
            .GroupBy(m => m.PartitionKey)
            .Select(g => new { PartitionKey = g.Key, MinSequence = g.Min(m => m.Sequence) })
            .OrderBy(x => x.MinSequence)
            .Select(x => x.PartitionKey)
            .Take(max)
            .ToListAsync(token);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingByPartitionAsync(Guid partitionKey, int max,
        CancellationToken token = default) => await _outboxMessages
        .Where(m => m.PartitionKey == partitionKey)
        .Where(IsPending)
        .OrderBy(m => m.Sequence)
        .Take(max)
        .ToListAsync(token);

    // Removes the row entirely rather than marking it — see IOutboxStore.MarkDispatchedAsync.
    public Task<bool> MarkDispatchedAsync(Guid outboxMessageId, long leaseVersion, CancellationToken token = default)
        => TryApplyAsync(outboxMessageId, leaseVersion, m => _outboxMessages.Remove(m), token);

    public Task<bool> MarkFailedAsync(Guid outboxMessageId, long leaseVersion, string error,
        CancellationToken token = default) => TryApplyAsync(outboxMessageId, leaseVersion, m =>
    {
        m.AttemptCount++;
        m.LastError = error;
    }, token);

    public Task<bool> MarkDeadLetteredAsync(Guid outboxMessageId, long leaseVersion, string reason,
        CancellationToken token = default) => TryApplyAsync(outboxMessageId, leaseVersion, m =>
    {
        m.DeadLetteredAt = DateTime.UtcNow;
        m.LastError = reason;
    }, token);


    public async Task DeleteDispatchedBeforeAsync(DateTime cutoff, CancellationToken token = default)
    {
        var cutOffs = await _outboxMessages
            .Where(m => m.DeadLetteredAt != null && m.DeadLetteredAt < cutoff)
            .ToArrayAsync(token);
        _outboxMessages.RemoveRange(cutOffs);
        await dbContext.SaveChangesAsync(token);
    }

    private static Expression<Func<OutboxMessage, bool>> IsPending => m => m.DeadLetteredAt == null;

    private async Task<bool> TryApplyAsync(Guid outboxMessageId, long leaseVersion, Action<OutboxMessage> apply,
        CancellationToken token)
    {
        var message = await _outboxMessages.FindAsync([outboxMessageId], token);
        if (message is null) return false;

        var lease = await dbContext.Set<OutboxPartitionLease>().FindAsync([message.PartitionKey], token);
        if (lease is null || lease.Version != leaseVersion) return false;

        apply.Invoke(message);
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
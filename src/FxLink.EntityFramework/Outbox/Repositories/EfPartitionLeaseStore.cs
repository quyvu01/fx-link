using FxLink.Abstractions;
using FxLink.EntityFramework.Outbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace FxLink.EntityFramework.Outbox.Repositories;

// SQL-backed IPartitionLeaseStore. Atomicity comes from OutboxPartitionLease.Version being an EF
// Core concurrency token (see ModelBuilderExtensions) — SaveChangesAsync throws
// DbUpdateConcurrencyException if the row changed since it was read, which is the fencing mechanism
// itself, not just a safety net on top of a hand-rolled comparison (contrast InMemoryPartitionLeaseStore,
// which has no database underneath it to enforce that and must do the comparison itself under a lock).
internal sealed class EfPartitionLeaseStore(DbContext dbContext) : IPartitionLeaseStore
{
    private readonly DbSet<OutboxPartitionLease> _outboxPartitionLeases = dbContext.Set<OutboxPartitionLease>();

    public async Task<long?> TryAcquireAsync(Guid partitionKey, string ownerId, TimeSpan leaseDuration,
        CancellationToken token = default)
    {
        var now = DateTime.UtcNow;
        var lease = await _outboxPartitionLeases.FindAsync([partitionKey], token);

        if (lease is null)
        {
            lease = new OutboxPartitionLease
            {
                PartitionKey = partitionKey, OwnerId = ownerId, LeasedUntil = now + leaseDuration, Version = 1
            };
            _outboxPartitionLeases.Add(lease);

            try
            {
                await dbContext.SaveChangesAsync(token);
                return lease.Version;
            }
            catch (DbUpdateException)
            {
                // Same reasoning as EfInboxStore.TryClaimAsync: DbUpdateException covers every SaveChanges
                // failure, not just another instance inserting the same PartitionKey first. Treating a
                // timeout or dropped connection as "theirs" would hide it — the partition would just be
                // skipped every tick with nothing logged. Drop the failed insert from the tracker and
                // only return null when the row really exists; otherwise let OutboxDispatcherWorker's
                // tick-level handler log it.
                dbContext.Entry(lease).State = EntityState.Detached;

                var heldElsewhere = await _outboxPartitionLeases.AsNoTracking()
                    .AnyAsync(l => l.PartitionKey == partitionKey, token);
                if (heldElsewhere) return null;

                throw;
            }
        }

        if (lease.LeasedUntil > now && lease.OwnerId != ownerId) return null;

        lease.OwnerId = ownerId;
        lease.LeasedUntil = now + leaseDuration;
        lease.Version++;

        try
        {
            await dbContext.SaveChangesAsync(token);
            return lease.Version;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public async Task<long?> RenewAsync(Guid partitionKey, string ownerId, long leaseVersion, TimeSpan leaseDuration,
        CancellationToken token = default)
    {
        var lease = await _outboxPartitionLeases.FindAsync([partitionKey], token);
        if (lease is null || lease.OwnerId != ownerId || lease.Version != leaseVersion) return null;

        lease.LeasedUntil = DateTime.UtcNow + leaseDuration;
        lease.Version++;

        try
        {
            await dbContext.SaveChangesAsync(token);
            return lease.Version;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public async Task ReleaseAsync(Guid partitionKey, string ownerId, long leaseVersion,
        CancellationToken token = default)
    {
        var lease = await _outboxPartitionLeases.FindAsync([partitionKey], token);
        if (lease is null || lease.OwnerId != ownerId || lease.Version != leaseVersion) return;

        lease.LeasedUntil = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Already reclaimed by someone else — nothing to release.
        }
    }
}
using FxLink.Abstractions;
using FxLink.EntityFramework.Inbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace FxLink.EntityFramework.Inbox.Repositories;

// SQL-backed IInboxStore. Atomicity comes from InboxRecord.Version being an EF Core concurrency
// token (see ModelBuilderExtensions) — SaveChangesAsync throws DbUpdateConcurrencyException if the
// row changed since it was read, which is the fencing mechanism itself, not just a safety net on top
// of a hand-rolled comparison (contrast InMemoryInboxStore, which has no database underneath it to
// enforce that and must do the comparison itself under a lock).
internal sealed class EfInboxStore(DbContext dbContext) : IInboxStore
{
    private readonly DbSet<InboxRecord> _inboxRecords = dbContext.Set<InboxRecord>();

    public async Task<long?> TryClaimAsync(string consumerKey, Guid messageId, TimeSpan claimDuration,
        CancellationToken token = default)
    {
        var now = DateTime.UtcNow;
        var record = await _inboxRecords.FindAsync([consumerKey, messageId], token);

        if (record is null)
        {
            record = new InboxRecord
            {
                ConsumerKey = consumerKey,
                MessageId = messageId,
                State = InboxRecordState.Claimed,
                ClaimedUntil = now + claimDuration,
                Version = 1
            };
            _inboxRecords.Add(record);

            try
            {
                await dbContext.SaveChangesAsync(token);
                return record.Version;
            }
            catch (DbUpdateException)
            {
                // DbUpdateException is how EF reports EVERY SaveChanges failure — a duplicate key from
                // another instance inserting the same (ConsumerKey, MessageId) concurrently, but also a
                // timeout, a dropped connection or a deadlock. Only the first means "theirs, not ours";
                // returning null for the rest would make the pipeline skip the message and the
                // transport ack it, losing it silently. So drop the failed insert from the tracker
                // (otherwise the next SaveChanges on this context would try to insert it again) and
                // confirm the row really exists before treating it as owned elsewhere.
                dbContext.Entry(record).State = EntityState.Detached;

                var ownedElsewhere = await _inboxRecords.AsNoTracking()
                    .AnyAsync(r => r.ConsumerKey == consumerKey && r.MessageId == messageId, token);
                if (ownedElsewhere) return null;

                throw;
            }
        }

        if (record.State == InboxRecordState.Processed) return null;
        if (record.ClaimedUntil > now) return null; // still fresh, owned elsewhere

        record.ClaimedUntil = now + claimDuration;
        record.Version++; // steal: expired claim, bump the fencing token and extend it

        try
        {
            await dbContext.SaveChangesAsync(token);
            return record.Version;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public async Task<long?> RenewClaimAsync(string consumerKey, Guid messageId, long claimVersion,
        TimeSpan claimDuration, CancellationToken token = default)
    {
        var record = await _inboxRecords.FindAsync([consumerKey, messageId], token);
        if (record is null || record.State != InboxRecordState.Claimed || record.Version != claimVersion)
            return null;

        record.ClaimedUntil = DateTime.UtcNow + claimDuration;
        record.Version++;

        try
        {
            await dbContext.SaveChangesAsync(token);
            return record.Version;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public async Task<bool> MarkProcessedAsync(string consumerKey, Guid messageId, long claimVersion,
        CancellationToken token = default)
    {
        var record = await _inboxRecords.FindAsync([consumerKey, messageId], token);
        if (record is null || record.State != InboxRecordState.Claimed || record.Version != claimVersion)
            return false;

        record.State = InboxRecordState.Processed;
        record.ProcessedAt = DateTime.UtcNow;

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

    public async Task ReleaseClaimAsync(string consumerKey, Guid messageId, long claimVersion,
        CancellationToken token = default)
    {
        var record = await _inboxRecords.FindAsync([consumerKey, messageId], token);
        if (record is null || record.State != InboxRecordState.Claimed || record.Version != claimVersion) return;

        // Safe to remove outright (unlike EfPartitionLeaseStore.ReleaseAsync, which expires in
        // place): a released claim was never Processed, so there's no history that a future
        // TryClaimAsync starting fresh at Version=1 could clobber or be confused with.
        _inboxRecords.Remove(record);

        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Already reclaimed by someone else — nothing to release.
        }
    }

    public async Task DeleteProcessedBeforeAsync(DateTime cutoff, CancellationToken token = default)
    {
        var processed = await _inboxRecords
            .Where(r => r.State == InboxRecordState.Processed && r.ProcessedAt != null && r.ProcessedAt < cutoff)
            .ToArrayAsync(token);
        _inboxRecords.RemoveRange(processed);
        await dbContext.SaveChangesAsync(token);
    }
}

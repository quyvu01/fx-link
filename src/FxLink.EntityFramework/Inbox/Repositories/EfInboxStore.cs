using FxLink.Abstractions;
using FxLink.EntityFramework.Inbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.EntityFramework.Inbox.Repositories;

// SQL-backed IInboxStore. Atomicity comes from InboxRecord.Version being an EF Core concurrency
// token (see ModelBuilderExtensions) — SaveChangesAsync throws DbUpdateConcurrencyException if the
// row changed since it was read, which is the fencing mechanism itself, not just a safety net on top
// of a hand-rolled comparison (contrast InMemoryInboxStore, which has no database underneath it to
// enforce that and must do the comparison itself under a lock).
//
// dbContext is, by default, the very same scoped DbContext the consumer itself writes through, which
// is deliberate for MarkProcessedAsync: its SaveChanges commits the consumer's changes together with
// the Processed mark. Everything else must not leak the consumer's unsaved changes, though:
// - RenewClaimAsync runs on a background loop WHILE the consumer runs, so it gets its own DbContext
//   from a fresh scope (renewalContextResolver) — sharing would either commit the consumer's half-done
//   changes or throw EF's "a second operation was started on this context".
// - ReleaseClaimAsync runs after the consumer threw, so it discards whatever the consumer changed but
//   didn't save before its own SaveChanges — otherwise the failed attempt's changes would be committed
//   and then applied again by the retry.
// Without a scope factory (direct construction in tests) renewal falls back to dbContext itself.
internal sealed class EfInboxStore(
    DbContext dbContext,
    IServiceScopeFactory scopeFactory = null,
    Func<IServiceProvider, DbContext> renewalContextResolver = null) : IInboxStore
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
        if (scopeFactory is null || renewalContextResolver is null)
            return await RenewClaimAsync(dbContext, consumerKey, messageId, claimVersion, claimDuration, token);

        await using var scope = scopeFactory.CreateAsyncScope();
        var renewalContext = renewalContextResolver(scope.ServiceProvider);
        try
        {
            return await RenewClaimAsync(renewalContext, consumerKey, messageId, claimVersion, claimDuration, token);
        }
        finally
        {
            // A context the container owns is disposed with the scope anyway (disposing twice is a no-op);
            // one built by a user-supplied factory isn't, so dispose it here explicitly.
            await renewalContext.DisposeAsync();
        }
    }

    private static async Task<long?> RenewClaimAsync(DbContext context, string consumerKey, Guid messageId,
        long claimVersion, TimeSpan claimDuration, CancellationToken token)
    {
        var record = await context.Set<InboxRecord>().FindAsync([consumerKey, messageId], token);
        if (record is null || record.State != InboxRecordState.Claimed || record.Version != claimVersion)
            return null;

        record.ClaimedUntil = DateTime.UtcNow + claimDuration;
        record.Version++;

        try
        {
            await context.SaveChangesAsync(token);
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
        var record = await LoadCurrentAsync(consumerKey, messageId, token);
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
        // The consumer threw: drop everything it changed but didn't save (its entities, any Outbox rows it
        // enqueued, ...) so the SaveChanges below commits only the release, the same as a rollback would.
        // Changes the consumer already saved itself can't be undone here.
        dbContext.ChangeTracker.Clear();

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

    // Set-based delete: never loads the rows, however many have piled up since the last sweep.
    public Task DeleteProcessedBeforeAsync(DateTime cutoff, CancellationToken token = default) =>
        _inboxRecords
            .Where(r => r.State == InboxRecordState.Processed && r.ProcessedAt != null && r.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(token);

    // The record this context tracks since TryClaimAsync can be stale by now: renewal bumps Version through
    // its own DbContext. Reload it so the version check (and the concurrency token SaveChanges compares
    // against) reflects the database, not the claim-time snapshot.
    private async Task<InboxRecord> LoadCurrentAsync(string consumerKey, Guid messageId, CancellationToken token)
    {
        var tracked = _inboxRecords.Local
            .FirstOrDefault(r => r.ConsumerKey == consumerKey && r.MessageId == messageId);
        if (tracked is null) return await _inboxRecords.FindAsync([consumerKey, messageId], token);

        var entry = dbContext.Entry(tracked);
        await entry.ReloadAsync(token);
        return entry.State == EntityState.Detached ? null : tracked; // Detached: the row no longer exists
    }
}

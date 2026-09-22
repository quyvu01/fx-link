using FxLink.Messaging.EntityFrameworkCore.Inbox.Entities;
using FxLink.Messaging.EntityFrameworkCore.Inbox.Repositories;
using FxLink.Messaging.EntityFrameworkCore.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace FxLink.Messaging.EntityFrameworkCore.Tests.Inbox.Repositories;

public class EfInboxStoreTests
{
    private const string ConsumerKey = "Consumers.OrderCreatedConsumer";

    [Fact]
    public async Task TryClaimAsync_creates_a_new_record_with_version_1_when_none_exists()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);

        var version = await store.TryClaimAsync(ConsumerKey, Guid.NewGuid(), TimeSpan.FromMinutes(1));

        version.ShouldBe(1);
    }

    [Fact]
    public async Task TryClaimAsync_returns_null_when_still_claimed_and_unexpired()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();

        await using (var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true))
            await new EfInboxStore(context).TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        await using var otherContext = SqliteDbContextFactory.CreateInboxContext(connection);
        var version = await new EfInboxStore(otherContext)
            .TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        version.ShouldBeNull();
    }

    [Fact]
    public async Task TryClaimAsync_returns_null_when_already_processed()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();

        await using (var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true))
        {
            var store = new EfInboxStore(context);
            var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));
            await store.MarkProcessedAsync(ConsumerKey, messageId, claimed!.Value);
        }

        await using var otherContext = SqliteDbContextFactory.CreateInboxContext(connection);
        var version = await new EfInboxStore(otherContext)
            .TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        version.ShouldBeNull();
    }

    [Fact]
    public async Task TryClaimAsync_steals_and_bumps_the_version_once_the_claim_has_expired()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();

        // Seed an already-expired claim directly, since TryClaimAsync always grants a forward-dated
        // claim — an expiry can't be produced by calling it twice.
        await using (var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true))
        {
            context.Set<InboxRecord>().Add(new InboxRecord
            {
                ConsumerKey = ConsumerKey,
                MessageId = messageId,
                State = InboxRecordState.Claimed,
                ClaimedUntil = DateTime.UtcNow.AddMinutes(-1),
                Version = 1
            });
            await context.SaveChangesAsync();
        }

        await using var otherContext = SqliteDbContextFactory.CreateInboxContext(connection);
        var version = await new EfInboxStore(otherContext)
            .TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        version.ShouldBe(2);
    }

    [Fact]
    public async Task RenewClaimAsync_bumps_the_version_when_the_claim_version_matches()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        var renewed = await store.RenewClaimAsync(ConsumerKey, messageId, claimed!.Value, TimeSpan.FromMinutes(1));

        renewed.ShouldBe(claimed.Value + 1);
    }

    [Fact]
    public async Task RenewClaimAsync_returns_null_when_the_caller_holds_a_stale_version()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));
        await store.RenewClaimAsync(ConsumerKey, messageId, claimed!.Value, TimeSpan.FromMinutes(1));

        // claimed.Value is now one version behind the row's current version.
        var renewed = await store.RenewClaimAsync(ConsumerKey, messageId, claimed.Value, TimeSpan.FromMinutes(1));

        renewed.ShouldBeNull();
    }

    [Fact]
    public async Task MarkProcessedAsync_transitions_the_record_when_the_claim_version_matches()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        var result = await store.MarkProcessedAsync(ConsumerKey, messageId, claimed!.Value);

        result.ShouldBeTrue();

        await using var otherContext = SqliteDbContextFactory.CreateInboxContext(connection);
        var record = await otherContext.Set<InboxRecord>().FindAsync(ConsumerKey, messageId);
        record!.State.ShouldBe(InboxRecordState.Processed);
        record.ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkProcessedAsync_returns_false_when_the_claim_version_is_stale()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        var result = await store.MarkProcessedAsync(ConsumerKey, messageId, claimed!.Value + 1);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ReleaseClaimAsync_removes_the_record_entirely()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        await store.ReleaseClaimAsync(ConsumerKey, messageId, claimed!.Value);

        await using var otherContext = SqliteDbContextFactory.CreateInboxContext(connection);
        var record = await otherContext.Set<InboxRecord>().FindAsync(ConsumerKey, messageId);
        record.ShouldBeNull();
    }

    [Fact]
    public async Task ReleaseClaimAsync_is_a_noop_when_the_claim_version_is_stale()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        await Should.NotThrowAsync(() => store.ReleaseClaimAsync(ConsumerKey, messageId, claimed!.Value + 1));

        await using var otherContext = SqliteDbContextFactory.CreateInboxContext(connection);
        var record = await otherContext.Set<InboxRecord>().FindAsync(ConsumerKey, messageId);
        record.ShouldNotBeNull(); // still present, not removed
    }

    [Fact]
    public async Task DeleteProcessedBeforeAsync_removes_only_processed_records_past_the_cutoff()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);

        var cutoff = DateTime.UtcNow;

        var oldProcessed = new InboxRecord
        {
            ConsumerKey = ConsumerKey, MessageId = Guid.NewGuid(), State = InboxRecordState.Processed,
            ProcessedAt = cutoff.AddDays(-1), Version = 1
        };
        var recentProcessed = new InboxRecord
        {
            ConsumerKey = ConsumerKey, MessageId = Guid.NewGuid(), State = InboxRecordState.Processed,
            ProcessedAt = cutoff.AddMinutes(1), Version = 1
        };
        var stillClaimed = new InboxRecord
        {
            ConsumerKey = ConsumerKey, MessageId = Guid.NewGuid(), State = InboxRecordState.Claimed,
            ClaimedUntil = cutoff.AddMinutes(1), Version = 1
        };

        context.Set<InboxRecord>().AddRange(oldProcessed, recentProcessed, stillClaimed);
        await context.SaveChangesAsync();

        var store = new EfInboxStore(context);
        await store.DeleteProcessedBeforeAsync(cutoff);

        await using var afterContext = SqliteDbContextFactory.CreateInboxContext(connection);
        var remainingIds = await afterContext.Set<InboxRecord>().Select(r => r.MessageId).ToListAsync();
        remainingIds.ShouldBe([recentProcessed.MessageId, stillClaimed.MessageId], ignoreOrder: true);
    }
}

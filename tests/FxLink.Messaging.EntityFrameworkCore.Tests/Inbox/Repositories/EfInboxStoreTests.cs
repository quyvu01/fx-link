using FxLink.EntityFramework.Inbox.Entities;
using FxLink.EntityFramework.Inbox.Repositories;
using FxLink.EntityFramework.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.EntityFramework.Tests.Inbox.Repositories;

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
    public async Task TryClaimAsync_returns_null_and_leaves_nothing_tracked_when_another_instance_inserts_first()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var messageId = Guid.NewGuid();
        await using (SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true))
        {
        }

        // Just before this context's INSERT goes out, another instance wins the race for the same key.
        var racing = new RunBeforeSaveInterceptor(async () =>
        {
            await using var winner = SqliteDbContextFactory.CreateInboxContext(connection);
            await new EfInboxStore(winner).TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));
        });
        await using var context = CreateInboxContext(connection, racing);

        var version = await new EfInboxStore(context)
            .TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        version.ShouldBeNull();
        context.ChangeTracker.Entries<InboxRecord>().ShouldBeEmpty();
    }

    [Fact]
    public async Task TryClaimAsync_rethrows_a_database_failure_that_is_not_a_duplicate_key()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using (SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true))
        {
        }

        var failing = new RunBeforeSaveInterceptor(() => throw new DbUpdateException("database unavailable"));
        await using var context = CreateInboxContext(connection, failing);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => new EfInboxStore(context)
            .TryClaimAsync(ConsumerKey, Guid.NewGuid(), TimeSpan.FromMinutes(1)));

        exception.Message.ShouldBe("database unavailable");
    }

    private static TestInboxDbContext CreateInboxContext(SqliteConnection connection,
        SaveChangesInterceptor interceptor) => new(new DbContextOptionsBuilder<TestInboxDbContext>()
        .UseSqlite(connection).AddInterceptors(interceptor).Options);

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

    // The DI shape: renewal resolves its own DbContext from a fresh scope, here sharing the same SQLite
    // connection the way two scoped contexts share one database.
    private static (EfInboxStore Store, ServiceProvider Provider) CreateScopedStore(SqliteConnection connection,
        TestInboxDbContext context)
    {
        var provider = new ServiceCollection()
            .AddScoped(_ => SqliteDbContextFactory.CreateInboxContext(connection))
            .BuildServiceProvider();
        var store = new EfInboxStore(context, provider.GetRequiredService<IServiceScopeFactory>(),
            sp => sp.GetRequiredService<TestInboxDbContext>());
        return (store, provider);
    }

    private static async Task<List<string>> NoteTextsAsync(SqliteConnection connection)
    {
        await using var fresh = SqliteDbContextFactory.CreateInboxContext(connection);
        return await fresh.Notes.Select(n => n.Text).ToListAsync();
    }

    [Fact]
    public async Task ReleaseClaimAsync_does_not_commit_the_consumers_unsaved_changes()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        // The consumer changes its own data through the shared context, then throws before saving.
        context.Notes.Add(new TestNote { Id = Guid.NewGuid(), Text = "half-done" });
        await store.ReleaseClaimAsync(ConsumerKey, messageId, claimed!.Value);

        (await NoteTextsAsync(connection)).ShouldBeEmpty();
        (await context.Set<InboxRecord>().AsNoTracking().AnyAsync(r => r.MessageId == messageId)).ShouldBeFalse();
    }

    [Fact]
    public async Task RenewClaimAsync_uses_its_own_context_and_does_not_commit_the_consumers_unsaved_changes()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var (store, provider) = CreateScopedStore(connection, context);
        await using var _ = provider;
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        context.Notes.Add(new TestNote { Id = Guid.NewGuid(), Text = "in progress" });
        var renewed = await store.RenewClaimAsync(ConsumerKey, messageId, claimed!.Value, TimeSpan.FromMinutes(1));

        renewed.ShouldBe(claimed.Value + 1);
        (await NoteTextsAsync(connection)).ShouldBeEmpty();
        context.ChangeTracker.HasChanges().ShouldBeTrue(); // the consumer's pending change is left alone
    }

    [Fact]
    public async Task MarkProcessedAsync_succeeds_after_a_renewal_and_commits_the_consumers_changes_with_it()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var (store, provider) = CreateScopedStore(connection, context);
        await using var _ = provider;
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        // Renewal bumps the version through another context; the record this context tracks since the
        // claim is now stale and must be refreshed, not compared as-is.
        var renewed = await store.RenewClaimAsync(ConsumerKey, messageId, claimed!.Value, TimeSpan.FromMinutes(1));
        context.Notes.Add(new TestNote { Id = Guid.NewGuid(), Text = "done" });

        var marked = await store.MarkProcessedAsync(ConsumerKey, messageId, renewed!.Value);

        marked.ShouldBeTrue();
        (await NoteTextsAsync(connection)).ShouldBe(["done"]);
        await using var fresh = SqliteDbContextFactory.CreateInboxContext(connection);
        (await fresh.Set<InboxRecord>().SingleAsync(r => r.MessageId == messageId)).State
            .ShouldBe(InboxRecordState.Processed);
    }

    [Fact]
    public async Task MarkProcessedAsync_returns_false_when_the_record_was_removed_meanwhile()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateInboxContext(connection, ensureCreated: true);
        var store = new EfInboxStore(context);
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromMinutes(1));

        await using (var other = SqliteDbContextFactory.CreateInboxContext(connection))
            await other.Set<InboxRecord>().Where(r => r.MessageId == messageId).ExecuteDeleteAsync();

        (await store.MarkProcessedAsync(ConsumerKey, messageId, claimed!.Value)).ShouldBeFalse();
    }
}

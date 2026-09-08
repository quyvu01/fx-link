using FxLink.Entities;
using FxLink.Outbox.EntityFrameworkCore.Entities;
using FxLink.Outbox.EntityFrameworkCore.Repositories;
using FxLink.Outbox.EntityFrameworkCore.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace FxLink.Outbox.EntityFrameworkCore.Tests.Repositories;

public class EfOutboxStoreTests
{
    // SQLite has no equivalent of SqlServer IDENTITY/Postgres SERIAL for a non-PK column, so
    // Sequence's ValueGeneratedOnAdd() mapping never gets a DB-side default under the Sqlite
    // provider — production targets (SqlServer/Npgsql) do generate it. Assigning an explicit,
    // monotonically increasing value here stands in for what the real DB would assign, purely so
    // these tests can run against a real relational engine instead of the EF InMemory provider.
    private static long _sequenceCounter;

    private static OutboxMessage NewMessage(Guid partitionKey) => new()
    {
        PartitionKey = partitionKey,
        MessageType = "Test.Message",
        Payload = "{}",
        SerializedHeaders = "{}",
        Sequence = Interlocked.Increment(ref _sequenceCounter)
    };

    [Fact]
    public async Task EnqueueAsync_only_tracks_the_message_until_caller_calls_SaveChangesAsync()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var store = new EfOutboxStore(context);
        var message = NewMessage(Guid.NewGuid());

        await store.EnqueueAsync(message);

        await using (var otherContext = SqliteDbContextFactory.CreateContext(connection))
        {
            var notYetPersisted = await otherContext.Set<OutboxMessage>().FindAsync(message.Id);
            notYetPersisted.ShouldBeNull();
        }

        await context.SaveChangesAsync();

        await using var afterSaveContext = SqliteDbContextFactory.CreateContext(connection);
        var persisted = await afterSaveContext.Set<OutboxMessage>().FindAsync(message.Id);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPendingPartitionKeysAsync_orders_by_earliest_pending_sequence_per_partition()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var store = new EfOutboxStore(context);

        var partitionA = Guid.NewGuid();
        var partitionB = Guid.NewGuid();

        // Insertion order drives Sequence order: A, B, A — a partition's priority must be its
        // EARLIEST pending Sequence, so A (seq 1) must sort before B (seq 2) even though A also
        // has a later row (seq 3). Regression for the GroupBy+Min fix: an OrderBy placed before
        // Distinct doesn't reliably survive SQL translation.
        await store.EnqueueAsync(NewMessage(partitionA));
        await context.SaveChangesAsync();
        await store.EnqueueAsync(NewMessage(partitionB));
        await context.SaveChangesAsync();
        await store.EnqueueAsync(NewMessage(partitionA));
        await context.SaveChangesAsync();

        var keys = await store.GetPendingPartitionKeysAsync(max: 10);

        keys.ShouldBe([partitionA, partitionB]);
    }

    [Fact]
    public async Task GetPendingPartitionKeysAsync_excludes_dead_lettered_partitions()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);

        var deadLetteredPartition = Guid.NewGuid();
        var pendingPartition = Guid.NewGuid();

        var deadLetteredMessage = NewMessage(deadLetteredPartition);
        deadLetteredMessage.DeadLetteredAt = DateTime.UtcNow;
        var pendingMessage = NewMessage(pendingPartition);

        context.Set<OutboxMessage>().AddRange(deadLetteredMessage, pendingMessage);
        await context.SaveChangesAsync();

        var store = new EfOutboxStore(context);
        var keys = await store.GetPendingPartitionKeysAsync(max: 10);

        keys.ShouldBe([pendingPartition]);
    }

    [Fact]
    public async Task GetPendingByPartitionAsync_returns_only_rows_for_that_partition_ordered_by_sequence()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var store = new EfOutboxStore(context);

        var target = Guid.NewGuid();
        var other = Guid.NewGuid();

        var first = NewMessage(target);
        await store.EnqueueAsync(first);
        await context.SaveChangesAsync();
        await store.EnqueueAsync(NewMessage(other));
        await context.SaveChangesAsync();
        var second = NewMessage(target);
        await store.EnqueueAsync(second);
        await context.SaveChangesAsync();

        var messages = await store.GetPendingByPartitionAsync(target, max: 10);

        messages.Select(m => m.Id).ShouldBe([first.Id, second.Id]);
    }

    private static async Task<(TestOutboxDbContext Context, EfOutboxStore Store, OutboxMessage Message)> SeedLeasedMessageAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection, long leaseVersion)
    {
        var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var partitionKey = Guid.NewGuid();
        var message = NewMessage(partitionKey);
        context.Set<OutboxMessage>().Add(message);
        context.Set<OutboxPartitionLease>().Add(new OutboxPartitionLease
        {
            PartitionKey = partitionKey,
            OwnerId = "owner-a",
            LeasedUntil = DateTime.UtcNow.AddMinutes(1),
            Version = leaseVersion
        });
        await context.SaveChangesAsync();
        return (context, new EfOutboxStore(context), message);
    }

    [Fact]
    public async Task MarkDispatchedAsync_removes_the_row_entirely_when_fencing_token_matches()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var (context, store, message) = await SeedLeasedMessageAsync(connection, leaseVersion: 1);
        await using var _ = context;

        var result = await store.MarkDispatchedAsync(message.Id, leaseVersion: 1);

        result.ShouldBeTrue();

        await using var otherContext = SqliteDbContextFactory.CreateContext(connection);
        var persisted = await otherContext.Set<OutboxMessage>().FindAsync(message.Id);
        persisted.ShouldBeNull();
    }

    [Fact]
    public async Task MarkDispatchedAsync_returns_false_and_leaves_the_row_in_place_when_fencing_token_is_stale()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var (context, store, message) = await SeedLeasedMessageAsync(connection, leaseVersion: 1);
        await using var _ = context;

        var result = await store.MarkDispatchedAsync(message.Id, leaseVersion: 2);

        result.ShouldBeFalse();

        await using var otherContext = SqliteDbContextFactory.CreateContext(connection);
        var persisted = await otherContext.Set<OutboxMessage>().FindAsync(message.Id);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkFailedAsync_increments_attempt_count_and_records_the_error()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var (context, store, message) = await SeedLeasedMessageAsync(connection, leaseVersion: 1);
        await using var _ = context;

        var result = await store.MarkFailedAsync(message.Id, leaseVersion: 1, error: "boom");

        result.ShouldBeTrue();
        message.AttemptCount.ShouldBe(1);
        message.LastError.ShouldBe("boom");
    }

    [Fact]
    public async Task MarkDeadLetteredAsync_sets_DeadLetteredAt_and_reason()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var (context, store, message) = await SeedLeasedMessageAsync(connection, leaseVersion: 1);
        await using var _ = context;

        var result = await store.MarkDeadLetteredAsync(message.Id, leaseVersion: 1, reason: "poison");

        result.ShouldBeTrue();
        message.DeadLetteredAt.ShouldNotBeNull();
        message.LastError.ShouldBe("poison");
    }

    [Fact]
    public async Task DeleteDispatchedBeforeAsync_removes_only_dead_lettered_rows_past_the_cutoff()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);

        var cutoff = DateTime.UtcNow;

        var oldDeadLettered = NewMessage(Guid.NewGuid());
        oldDeadLettered.DeadLetteredAt = cutoff.AddDays(-1);

        var recentDeadLettered = NewMessage(Guid.NewGuid());
        recentDeadLettered.DeadLetteredAt = cutoff.AddMinutes(1);

        var stillPending = NewMessage(Guid.NewGuid());

        context.Set<OutboxMessage>().AddRange(oldDeadLettered, recentDeadLettered, stillPending);
        await context.SaveChangesAsync();

        var store = new EfOutboxStore(context);
        await store.DeleteDispatchedBeforeAsync(cutoff);

        await using var afterContext = SqliteDbContextFactory.CreateContext(connection);
        var remainingIds = await afterContext.Set<OutboxMessage>().Select(m => m.Id).ToListAsync();
        remainingIds.ShouldBe([recentDeadLettered.Id, stillPending.Id], ignoreOrder: true);
    }
}

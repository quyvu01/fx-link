using FxLink.Entities;
using FxLink.InMemory;
using Shouldly;
using Xunit;

namespace FxLink.Tests.InMemory;

public class InMemoryOutboxStoreTests
{
    private static (InMemoryPartitionLeaseStore Leases, InMemoryOutboxStore Outbox) CreateStore()
    {
        var leases = new InMemoryPartitionLeaseStore();
        return (leases, new InMemoryOutboxStore(leases));
    }

    private static OutboxMessage NewMessage(Guid partitionKey) => new()
    {
        PartitionKey = partitionKey,
        MessageType = "Test",
        Payload = "{}"
    };

    [Fact]
    public async Task EnqueueAsync_assigns_a_monotonically_increasing_sequence_across_all_partitions()
    {
        var (_, outbox) = CreateStore();
        var partitionA = Guid.NewGuid();
        var partitionB = Guid.NewGuid();

        var first = NewMessage(partitionA);
        var second = NewMessage(partitionB);
        var third = NewMessage(partitionA);

        await outbox.EnqueueAsync(first);
        await outbox.EnqueueAsync(second);
        await outbox.EnqueueAsync(third);

        first.Sequence.ShouldBe(1);
        second.Sequence.ShouldBe(2);
        third.Sequence.ShouldBe(3);
    }

    [Fact]
    public async Task GetPendingPartitionKeysAsync_returns_distinct_keys_with_pending_messages_only()
    {
        var (leases, outbox) = CreateStore();
        var partitionA = Guid.NewGuid();
        var partitionB = Guid.NewGuid();

        var a1 = NewMessage(partitionA);
        var a2 = NewMessage(partitionA);
        var b1 = NewMessage(partitionB);
        await outbox.EnqueueAsync(a1);
        await outbox.EnqueueAsync(a2);
        await outbox.EnqueueAsync(b1);

        // Fully dispatch partitionB so it should drop out of the pending-partitions list.
        var version = await leases.TryAcquireAsync(partitionB, "owner", TimeSpan.FromSeconds(30));
        await outbox.MarkDispatchedAsync(b1.Id, version!.Value);

        var keys = await outbox.GetPendingPartitionKeysAsync(max: 10);

        keys.ShouldBe([partitionA]);
    }

    [Fact]
    public async Task GetPendingByPartitionAsync_returns_only_that_partitions_messages_ordered_by_sequence()
    {
        var (_, outbox) = CreateStore();
        var partitionA = Guid.NewGuid();
        var partitionB = Guid.NewGuid();

        var a1 = NewMessage(partitionA);
        var b1 = NewMessage(partitionB);
        var a2 = NewMessage(partitionA);
        await outbox.EnqueueAsync(a1);
        await outbox.EnqueueAsync(b1);
        await outbox.EnqueueAsync(a2);

        var pending = await outbox.GetPendingByPartitionAsync(partitionA, max: 10);

        pending.Select(m => m.Id).ShouldBe([a1.Id, a2.Id]);
    }

    [Fact]
    public async Task GetPendingByPartitionAsync_respects_the_max_parameter()
    {
        var (_, outbox) = CreateStore();
        var partitionKey = Guid.NewGuid();
        for (var i = 0; i < 5; i++) await outbox.EnqueueAsync(NewMessage(partitionKey));

        var pending = await outbox.GetPendingByPartitionAsync(partitionKey, max: 2);

        pending.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MarkDispatchedAsync_with_a_valid_lease_version_succeeds_and_removes_the_message_from_pending()
    {
        var (leases, outbox) = CreateStore();
        var partitionKey = Guid.NewGuid();
        var message = NewMessage(partitionKey);
        await outbox.EnqueueAsync(message);
        var version = await leases.TryAcquireAsync(partitionKey, "owner", TimeSpan.FromSeconds(30));

        var result = await outbox.MarkDispatchedAsync(message.Id, version!.Value);

        result.ShouldBeTrue();
        (await outbox.GetPendingByPartitionAsync(partitionKey, 10)).ShouldBeEmpty();
        outbox.Contains(message.Id).ShouldBeFalse(); // removed entirely, not just excluded from pending
    }

    [Fact]
    public async Task MarkDispatchedAsync_with_a_stale_lease_version_fails_and_leaves_the_message_pending()
    {
        var (leases, outbox) = CreateStore();
        var partitionKey = Guid.NewGuid();
        var message = NewMessage(partitionKey);
        await outbox.EnqueueAsync(message);

        // owner-a's lease is stolen by owner-b before owner-a gets to Mark the message.
        var staleVersion = await leases.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMilliseconds(30));
        await Task.Delay(60);
        await leases.TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromSeconds(30));

        var result = await outbox.MarkDispatchedAsync(message.Id, staleVersion!.Value);

        result.ShouldBeFalse();
        (await outbox.GetPendingByPartitionAsync(partitionKey, 10)).ShouldContain(m => m.Id == message.Id);
    }

    [Fact]
    public async Task MarkDispatchedAsync_returns_false_for_an_unknown_message_id()
    {
        var (_, outbox) = CreateStore();

        var result = await outbox.MarkDispatchedAsync(Guid.NewGuid(), leaseVersion: 1);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkFailedAsync_increments_attempt_count_and_records_the_error_but_keeps_the_message_pending()
    {
        var (leases, outbox) = CreateStore();
        var partitionKey = Guid.NewGuid();
        var message = NewMessage(partitionKey);
        await outbox.EnqueueAsync(message);
        var version = await leases.TryAcquireAsync(partitionKey, "owner", TimeSpan.FromSeconds(30));

        var result = await outbox.MarkFailedAsync(message.Id, version!.Value, "boom");

        result.ShouldBeTrue();
        var pending = await outbox.GetPendingByPartitionAsync(partitionKey, 10);
        pending.Count.ShouldBe(1);
        pending[0].AttemptCount.ShouldBe(1);
        pending[0].LastError.ShouldBe("boom");
    }

    [Fact]
    public async Task MarkDeadLetteredAsync_removes_the_message_from_pending_and_unblocks_the_next_message_in_the_partition()
    {
        var (leases, outbox) = CreateStore();
        var partitionKey = Guid.NewGuid();
        var poison = NewMessage(partitionKey);
        var next = NewMessage(partitionKey);
        await outbox.EnqueueAsync(poison);
        await outbox.EnqueueAsync(next);
        var version = await leases.TryAcquireAsync(partitionKey, "owner", TimeSpan.FromSeconds(30));

        var result = await outbox.MarkDeadLetteredAsync(poison.Id, version!.Value, "unrecoverable");

        result.ShouldBeTrue();
        var pending = await outbox.GetPendingByPartitionAsync(partitionKey, 10);
        pending.Select(m => m.Id).ShouldBe([next.Id]);
    }

    [Fact]
    public async Task DeleteDispatchedBeforeAsync_removes_only_dead_lettered_messages_older_than_the_cutoff()
    {
        var (leases, outbox) = CreateStore();
        var partitionKey = Guid.NewGuid();
        var oldDeadLettered = NewMessage(partitionKey);
        var recentDeadLettered = NewMessage(partitionKey);
        var stillPending = NewMessage(partitionKey);
        await outbox.EnqueueAsync(oldDeadLettered);
        await outbox.EnqueueAsync(recentDeadLettered);
        await outbox.EnqueueAsync(stillPending);

        var version = await leases.TryAcquireAsync(partitionKey, "owner", TimeSpan.FromSeconds(30));
        await outbox.MarkDeadLetteredAsync(oldDeadLettered.Id, version!.Value, "poison");
        await Task.Delay(50);
        var cutoff = DateTime.UtcNow;
        await Task.Delay(50);
        await outbox.MarkDeadLetteredAsync(recentDeadLettered.Id, version.Value, "poison");

        await outbox.DeleteDispatchedBeforeAsync(cutoff);

        outbox.Contains(oldDeadLettered.Id).ShouldBeFalse();
        outbox.Contains(recentDeadLettered.Id).ShouldBeTrue();
        outbox.Contains(stillPending.Id).ShouldBeTrue();
    }
}

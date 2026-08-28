using FxLink.InMemory;
using Shouldly;
using Xunit;

namespace FxLink.Tests.InMemory;

public class InMemoryPartitionLeaseStoreTests
{
    [Fact]
    public async Task TryAcquireAsync_grants_a_lease_on_a_free_partition_and_starts_at_version_one()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();

        var version = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromSeconds(30));

        version.ShouldBe(1);
        store.IsCurrentVersion(partitionKey, 1).ShouldBeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_fails_when_another_owner_already_holds_a_live_lease()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();

        await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromSeconds(30));
        var version = await store.TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromSeconds(30));

        version.ShouldBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_is_reentrant_for_the_same_owner_and_bumps_the_version()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();

        var first = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromSeconds(30));
        var second = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromSeconds(30));

        first.ShouldBe(1);
        second.ShouldBe(2);
    }

    [Fact]
    public async Task TryAcquireAsync_succeeds_for_a_new_owner_once_the_previous_lease_expires()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();

        await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMilliseconds(30));
        await Task.Delay(60);

        var version = await store.TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromSeconds(30));

        version.ShouldBe(2);
    }

    [Fact]
    public async Task Different_partitions_are_leased_independently()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionA = Guid.NewGuid();
        var partitionB = Guid.NewGuid();

        await store.TryAcquireAsync(partitionA, "owner-a", TimeSpan.FromSeconds(30));
        var versionB = await store.TryAcquireAsync(partitionB, "owner-b", TimeSpan.FromSeconds(30));

        versionB.ShouldBe(1);
    }

    [Fact]
    public async Task RenewAsync_extends_the_lease_and_returns_a_new_version()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();
        var acquired = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMilliseconds(50));

        var renewed = await store.RenewAsync(partitionKey, "owner-a", acquired!.Value, TimeSpan.FromSeconds(30));

        renewed.ShouldBe(acquired.Value + 1);

        // Would have expired by now if RenewAsync hadn't extended it.
        await Task.Delay(80);
        store.IsCurrentVersion(partitionKey, renewed!.Value).ShouldBeTrue();
    }

    [Fact]
    public async Task RenewAsync_fails_when_the_version_is_stale()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();
        var acquired = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromSeconds(30));
        await store.RenewAsync(partitionKey, "owner-a", acquired!.Value, TimeSpan.FromSeconds(30));

        // acquired.Value is now stale — the renew above already bumped the version.
        var result = await store.RenewAsync(partitionKey, "owner-a", acquired.Value, TimeSpan.FromSeconds(30));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RenewAsync_fails_when_a_different_owner_holds_the_lease()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();
        var acquired = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromSeconds(30));

        var result = await store.RenewAsync(partitionKey, "owner-b", acquired!.Value, TimeSpan.FromSeconds(30));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RenewAsync_fails_when_the_partition_was_never_acquired()
    {
        var store = new InMemoryPartitionLeaseStore();

        var result = await store.RenewAsync(Guid.NewGuid(), "owner-a", 1, TimeSpan.FromSeconds(30));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReleaseAsync_frees_the_partition_immediately_for_another_owner()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();
        var acquired = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromSeconds(30));

        await store.ReleaseAsync(partitionKey, "owner-a", acquired!.Value);
        var version = await store.TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromSeconds(30));

        version.ShouldBe(2);
    }

    [Fact]
    public async Task ReleaseAsync_with_a_stale_version_does_not_release_someone_elses_lease()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();
        var staleVersion = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMilliseconds(30));
        await Task.Delay(60);
        var currentVersion = await store.TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromSeconds(30));

        // owner-a doesn't know it lost the lease and tries to release its old version.
        await store.ReleaseAsync(partitionKey, "owner-a", staleVersion!.Value);

        store.IsCurrentVersion(partitionKey, currentVersion!.Value).ShouldBeTrue();
    }

    [Fact]
    public async Task IsCurrentVersion_reflects_the_latest_acquired_or_renewed_version()
    {
        var store = new InMemoryPartitionLeaseStore();
        var partitionKey = Guid.NewGuid();

        store.IsCurrentVersion(partitionKey, 1).ShouldBeFalse();

        var acquired = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromSeconds(30));
        store.IsCurrentVersion(partitionKey, acquired!.Value).ShouldBeTrue();

        var renewed = await store.RenewAsync(partitionKey, "owner-a", acquired.Value, TimeSpan.FromSeconds(30));
        store.IsCurrentVersion(partitionKey, acquired.Value).ShouldBeFalse();
        store.IsCurrentVersion(partitionKey, renewed!.Value).ShouldBeTrue();
    }
}

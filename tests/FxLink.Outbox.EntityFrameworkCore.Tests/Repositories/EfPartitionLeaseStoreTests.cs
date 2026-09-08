using FxLink.Outbox.EntityFrameworkCore.Entities;
using FxLink.Outbox.EntityFrameworkCore.Repositories;
using FxLink.Outbox.EntityFrameworkCore.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace FxLink.Outbox.EntityFrameworkCore.Tests.Repositories;

public class EfPartitionLeaseStoreTests
{
    [Fact]
    public async Task TryAcquireAsync_creates_a_new_lease_with_version_1_when_none_exists()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var store = new EfPartitionLeaseStore(context);

        var version = await store.TryAcquireAsync(Guid.NewGuid(), "owner-a", TimeSpan.FromMinutes(1));

        version.ShouldBe(1);
    }

    [Fact]
    public async Task TryAcquireAsync_returns_null_when_still_held_by_another_owner()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var partitionKey = Guid.NewGuid();

        await using (var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true))
            await new EfPartitionLeaseStore(context).TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMinutes(1));

        await using var otherContext = SqliteDbContextFactory.CreateContext(connection);
        var version = await new EfPartitionLeaseStore(otherContext)
            .TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromMinutes(1));

        version.ShouldBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_succeeds_and_bumps_the_version_once_the_lease_has_expired()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var partitionKey = Guid.NewGuid();

        // Seed an already-expired lease directly, since TryAcquireAsync always grants a
        // forward-dated lease — an expiry can't be produced by calling it twice.
        await using (var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true))
        {
            context.Set<OutboxPartitionLease>().Add(new OutboxPartitionLease
            {
                PartitionKey = partitionKey,
                OwnerId = "owner-a",
                LeasedUntil = DateTime.UtcNow.AddMinutes(-1),
                Version = 1
            });
            await context.SaveChangesAsync();
        }

        await using var otherContext = SqliteDbContextFactory.CreateContext(connection);
        var version = await new EfPartitionLeaseStore(otherContext)
            .TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromMinutes(1));

        version.ShouldBe(2);
    }

    [Fact]
    public async Task TryAcquireAsync_returns_null_when_another_instance_steals_the_lease_first()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var partitionKey = Guid.NewGuid();

        await using (var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true))
        {
            context.Set<OutboxPartitionLease>().Add(new OutboxPartitionLease
            {
                PartitionKey = partitionKey,
                OwnerId = "owner-a",
                LeasedUntil = DateTime.UtcNow.AddMinutes(-1),
                Version = 1
            });
            await context.SaveChangesAsync();
        }

        // Two separate DbContext instances (simulating two dispatcher instances) both load the
        // same expired lease before either writes back — a genuine optimistic-concurrency race.
        await using var contextA = SqliteDbContextFactory.CreateContext(connection);
        await using var contextB = SqliteDbContextFactory.CreateContext(connection);
        var storeA = new EfPartitionLeaseStore(contextA);
        var storeB = new EfPartitionLeaseStore(contextB);

        // Prime both contexts' change trackers by reading the row first.
        await contextA.Set<OutboxPartitionLease>().FindAsync(partitionKey);
        await contextB.Set<OutboxPartitionLease>().FindAsync(partitionKey);

        var winnerVersion = await storeA.TryAcquireAsync(partitionKey, "owner-a-again", TimeSpan.FromMinutes(1));
        var loserVersion = await storeB.TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromMinutes(1));

        winnerVersion.ShouldBe(2);
        loserVersion.ShouldBeNull(); // rejected by the concurrency token, not by an owner/expiry check
    }

    [Fact]
    public async Task RenewAsync_bumps_the_version_when_owner_and_version_match()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var partitionKey = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var store = new EfPartitionLeaseStore(context);
        var acquired = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMinutes(1));

        var renewed = await store.RenewAsync(partitionKey, "owner-a", acquired!.Value, TimeSpan.FromMinutes(1));

        renewed.ShouldBe(acquired.Value + 1);
    }

    [Fact]
    public async Task RenewAsync_returns_null_when_the_caller_holds_a_stale_version()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var partitionKey = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var store = new EfPartitionLeaseStore(context);
        var acquired = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMinutes(1));
        await store.RenewAsync(partitionKey, "owner-a", acquired!.Value, TimeSpan.FromMinutes(1));

        // acquired.Value is now one version behind the row's current version.
        var renewed = await store.RenewAsync(partitionKey, "owner-a", acquired.Value, TimeSpan.FromMinutes(1));

        renewed.ShouldBeNull();
    }

    [Fact]
    public async Task ReleaseAsync_expires_the_lease_in_place_instead_of_deleting_it()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var partitionKey = Guid.NewGuid();
        await using var context = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var store = new EfPartitionLeaseStore(context);
        var acquired = await store.TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMinutes(1));

        await store.ReleaseAsync(partitionKey, "owner-a", acquired!.Value);

        await using var otherContext = SqliteDbContextFactory.CreateContext(connection);
        var lease = await otherContext.Set<OutboxPartitionLease>().FindAsync(partitionKey);
        lease.ShouldNotBeNull(); // still present, not removed
        lease.LeasedUntil.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        lease.Version.ShouldBe(acquired.Value); // Version is never reused/reset by release
    }

    [Fact]
    public async Task ReleaseAsync_is_a_noop_when_the_lease_was_already_reclaimed_by_another_owner()
    {
        await using var connection = SqliteDbContextFactory.OpenConnection();
        var partitionKey = Guid.NewGuid();
        await using var seedContext = SqliteDbContextFactory.CreateContext(connection, ensureCreated: true);
        var acquired = await new EfPartitionLeaseStore(seedContext)
            .TryAcquireAsync(partitionKey, "owner-a", TimeSpan.FromMinutes(-1)); // already expired

        await using var stealerContext = SqliteDbContextFactory.CreateContext(connection);
        await new EfPartitionLeaseStore(stealerContext)
            .TryAcquireAsync(partitionKey, "owner-b", TimeSpan.FromMinutes(1));

        // owner-a releasing its now-stale version should not throw and should not affect owner-b's lease.
        await using var staleContext = SqliteDbContextFactory.CreateContext(connection);
        await Should.NotThrowAsync(() =>
            new EfPartitionLeaseStore(staleContext).ReleaseAsync(partitionKey, "owner-a", acquired!.Value));

        await using var verifyContext = SqliteDbContextFactory.CreateContext(connection);
        var lease = await verifyContext.Set<OutboxPartitionLease>().FindAsync(partitionKey);
        lease!.OwnerId.ShouldBe("owner-b");
    }
}

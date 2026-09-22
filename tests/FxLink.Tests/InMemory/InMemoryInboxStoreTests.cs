using FxLink.InMemory;
using Shouldly;
using Xunit;

namespace FxLink.Tests.InMemory;

public class InMemoryInboxStoreTests
{
    [Fact]
    public async Task TryClaimAsync_grants_a_claim_on_a_free_key_and_starts_at_version_one()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();

        var version = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));

        version.ShouldBe(1);
        store.Contains("ConsumerA", messageId).ShouldBeTrue();
    }

    [Fact]
    public async Task TryClaimAsync_fails_while_a_live_claim_is_already_held()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();

        await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));
        var second = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));

        second.ShouldBeNull();
    }

    [Fact]
    public async Task TryClaimAsync_steals_an_expired_claim_and_bumps_the_version()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();

        await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromMilliseconds(30));
        await Task.Delay(60);

        var version = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));

        version.ShouldBe(2);
    }

    [Fact]
    public async Task TryClaimAsync_returns_null_forever_once_the_message_has_been_marked_processed()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromMilliseconds(30));
        await store.MarkProcessedAsync("ConsumerA", messageId, claimed!.Value);

        // Wait past what would have been the claim's expiry — Processed must not become reclaimable.
        await Task.Delay(60);
        var version = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));

        version.ShouldBeNull();
    }

    [Fact]
    public async Task Fan_out_different_consumers_claim_the_same_message_independently()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();

        var versionA = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));
        var versionB = await store.TryClaimAsync("ConsumerB", messageId, TimeSpan.FromSeconds(30));

        versionA.ShouldBe(1);
        versionB.ShouldBe(1);
    }

    [Fact]
    public async Task Different_message_ids_for_the_same_consumer_are_claimed_independently()
    {
        var store = new InMemoryInboxStore();

        var versionA = await store.TryClaimAsync("ConsumerA", Guid.NewGuid(), TimeSpan.FromSeconds(30));
        var versionB = await store.TryClaimAsync("ConsumerA", Guid.NewGuid(), TimeSpan.FromSeconds(30));

        versionA.ShouldBe(1);
        versionB.ShouldBe(1);
    }

    [Fact]
    public async Task RenewClaimAsync_extends_the_claim_and_returns_a_new_version()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromMilliseconds(50));

        var renewed = await store.RenewClaimAsync("ConsumerA", messageId, claimed!.Value, TimeSpan.FromSeconds(30));

        renewed.ShouldBe(claimed.Value + 1);

        // Would have expired and become stealable by now if RenewClaimAsync hadn't extended it.
        await Task.Delay(80);
        var stolen = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));
        stolen.ShouldBeNull();
    }

    [Fact]
    public async Task RenewClaimAsync_fails_when_the_version_is_stale()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));
        await store.RenewClaimAsync("ConsumerA", messageId, claimed!.Value, TimeSpan.FromSeconds(30));

        // claimed.Value is now stale — the renew above already bumped the version.
        var result = await store.RenewClaimAsync("ConsumerA", messageId, claimed.Value, TimeSpan.FromSeconds(30));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RenewClaimAsync_fails_when_the_message_was_never_claimed()
    {
        var store = new InMemoryInboxStore();

        var result = await store.RenewClaimAsync("ConsumerA", Guid.NewGuid(), 1, TimeSpan.FromSeconds(30));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RenewClaimAsync_fails_once_the_message_has_been_marked_processed()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));
        await store.MarkProcessedAsync("ConsumerA", messageId, claimed!.Value);

        var result = await store.RenewClaimAsync("ConsumerA", messageId, claimed.Value, TimeSpan.FromSeconds(30));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task MarkProcessedAsync_succeeds_when_the_version_matches_the_live_claim()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));

        var result = await store.MarkProcessedAsync("ConsumerA", messageId, claimed!.Value);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task MarkProcessedAsync_fails_when_the_version_is_stale()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var staleClaim = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromMilliseconds(30));
        await Task.Delay(60);
        await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30)); // someone else steals it

        var result = await store.MarkProcessedAsync("ConsumerA", messageId, staleClaim!.Value);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ReleaseClaimAsync_removes_the_record_so_a_future_claim_starts_fresh_at_version_one()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));

        await store.ReleaseClaimAsync("ConsumerA", messageId, claimed!.Value);

        store.Contains("ConsumerA", messageId).ShouldBeFalse();
        var reclaimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));
        reclaimed.ShouldBe(1);
    }

    [Fact]
    public async Task ReleaseClaimAsync_with_a_stale_version_does_not_release_someone_elses_claim()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var staleClaim = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromMilliseconds(30));
        await Task.Delay(60);
        var currentClaim = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));

        await store.ReleaseClaimAsync("ConsumerA", messageId, staleClaim!.Value);

        // The current owner's claim must still be intact — renewing with the current version works.
        var renewed = await store.RenewClaimAsync("ConsumerA", messageId, currentClaim!.Value, TimeSpan.FromSeconds(30));
        renewed.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReleaseClaimAsync_never_removes_a_processed_record()
    {
        var store = new InMemoryInboxStore();
        var messageId = Guid.NewGuid();
        var claimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));
        await store.MarkProcessedAsync("ConsumerA", messageId, claimed!.Value);

        // Calling release with the (now-stale, since MarkProcessed doesn't bump version) claim
        // version must not touch a Processed record.
        await store.ReleaseClaimAsync("ConsumerA", messageId, claimed.Value);

        store.Contains("ConsumerA", messageId).ShouldBeTrue();
        var reclaimed = await store.TryClaimAsync("ConsumerA", messageId, TimeSpan.FromSeconds(30));
        reclaimed.ShouldBeNull(); // still permanently Processed
    }

    [Fact]
    public async Task DeleteProcessedBeforeAsync_removes_only_processed_records_older_than_the_cutoff()
    {
        var store = new InMemoryInboxStore();

        var oldProcessedId = Guid.NewGuid();
        var oldClaim = await store.TryClaimAsync("ConsumerA", oldProcessedId, TimeSpan.FromSeconds(30));
        await store.MarkProcessedAsync("ConsumerA", oldProcessedId, oldClaim!.Value);
        await Task.Delay(50);
        var cutoff = DateTime.UtcNow;
        await Task.Delay(50);

        var recentProcessedId = Guid.NewGuid();
        var recentClaim = await store.TryClaimAsync("ConsumerA", recentProcessedId, TimeSpan.FromSeconds(30));
        await store.MarkProcessedAsync("ConsumerA", recentProcessedId, recentClaim!.Value);

        var stillClaimedId = Guid.NewGuid();
        await store.TryClaimAsync("ConsumerA", stillClaimedId, TimeSpan.FromSeconds(30));

        await store.DeleteProcessedBeforeAsync(cutoff);

        store.Contains("ConsumerA", oldProcessedId).ShouldBeFalse();
        store.Contains("ConsumerA", recentProcessedId).ShouldBeTrue();
        store.Contains("ConsumerA", stillClaimedId).ShouldBeTrue();
    }
}

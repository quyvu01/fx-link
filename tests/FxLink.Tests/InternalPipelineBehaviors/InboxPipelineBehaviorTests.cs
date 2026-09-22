using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.InMemory;
using FxLink.InternalPipelineBehaviors;
using FxLink.Registries;
using FxLink.Wrappers;
using Shouldly;
using Xunit;

namespace FxLink.Tests.InternalPipelineBehaviors;

// Exercises InboxPipelineBehavior<TMessage> directly (constructed by hand, driven with hand-rolled
// ConsumerHandlerDelegate "next" callbacks) rather than through the full AddFxLink/orchestrator
// stack. That's deliberate: RetryPipelineBehavior is always registered globally by AddFxLink and,
// by design, swallows a normal consumer exception (converts it into a retry-republish) rather than
// letting it escape — so a full-stack test can never observe InboxPipelineBehavior's own
// exception-handling path. Testing the behavior in isolation is the correct, precise way to verify
// its own claim/renew/mark/release logic independent of whatever else happens to be registered.
public class InboxPipelineBehaviorTests
{
    private sealed record Payload(string Value);

    private sealed class FakeMessageKeys : IMessageKeys
    {
        public void AddMessageKey(Type messageType, Type messageKey)
        {
        }

        public Type[] GetKeysByMessageType(Type messageType) => [];
        public IReadOnlyDictionary<Type, Type[]> GetMessageKeys() => new Dictionary<Type, Type[]>();
    }

    private sealed class FakeInboxStoreResolver(IInboxStore store) : IInboxStoreResolver<Payload>
    {
        public IInboxStore GetInboxStore() => store;
    }

    private sealed class FakeConsumerType;

    private static IInboxOptions FastOptions => new InboxOptions
    {
        ClaimDuration = TimeSpan.FromSeconds(30), ClaimRenewInterval = TimeSpan.FromSeconds(30)
    };

    private static InboxPipelineBehavior<Payload> BehaviorFor(IInboxStore store, IInboxOptions options = null) =>
        new(new FakeInboxStoreResolver(store), options ?? FastOptions);

    private static IConsumeContext<Payload> ContextFor(Guid messageId, Type consumerType = null)
    {
        var context = new ConsumeContext<Payload>(new Payload("a"), new HeaderBag(), Guid.NewGuid(), null,
            DateTime.UtcNow, null, null, messageId);
        context.SetPayload(new ConsumerContextWrapped(consumerType ?? typeof(FakeConsumerType)));
        return context;
    }

    private static string ConsumerKey => typeof(FakeConsumerType).FullName!;

    [Fact]
    public async Task First_delivery_runs_next_and_marks_the_message_processed()
    {
        var store = new InMemoryInboxStore();
        var behavior = BehaviorFor(store);
        var context = ContextFor(Guid.NewGuid());
        var invocationCount = 0;

        await behavior.ConsumeAsync(context, _ =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        invocationCount.ShouldBe(1);
        store.Contains(ConsumerKey, context.MessageId).ShouldBeTrue();
    }

    [Fact]
    public async Task Redelivery_of_an_already_processed_message_does_not_run_next_again()
    {
        var store = new InMemoryInboxStore();
        var behavior = BehaviorFor(store);
        var messageId = Guid.NewGuid();
        var invocationCount = 0;
        ConsumerHandlerDelegate next = _ =>
        {
            invocationCount++;
            return Task.CompletedTask;
        };

        await behavior.ConsumeAsync(ContextFor(messageId), next);
        await behavior.ConsumeAsync(ContextFor(messageId), next);

        invocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_message_already_claimed_by_another_in_flight_delivery_is_skipped()
    {
        var store = new InMemoryInboxStore();
        var behavior = BehaviorFor(store);
        var messageId = Guid.NewGuid();
        var invocationCount = 0;

        // Simulate a concurrent in-flight delivery already owning the claim.
        await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromSeconds(30));

        await behavior.ConsumeAsync(ContextFor(messageId), _ =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        invocationCount.ShouldBe(0);
    }

    [Fact]
    public async Task An_exception_from_next_releases_the_claim_and_rethrows()
    {
        var store = new InMemoryInboxStore();
        var behavior = BehaviorFor(store);
        var messageId = Guid.NewGuid();
        ConsumerHandlerDelegate throwing = _ => throw new InvalidOperationException("boom");

        await Should.ThrowAsync<InvalidOperationException>(() => behavior.ConsumeAsync(ContextFor(messageId), throwing));

        store.Contains(ConsumerKey, messageId).ShouldBeFalse();

        // A subsequent delivery must be able to try again — the claim was fully released, not just
        // left behind in a failed state.
        var retryClaim = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromSeconds(30));
        retryClaim.ShouldBe(1);
    }

    [Fact]
    public async Task Renewal_keeps_a_long_running_consume_from_being_stolen()
    {
        var store = new InMemoryInboxStore();
        var shortOptions = new InboxOptions
        {
            ClaimDuration = TimeSpan.FromMilliseconds(80), ClaimRenewInterval = TimeSpan.FromMilliseconds(20)
        };
        var behavior = BehaviorFor(store, shortOptions);
        var messageId = Guid.NewGuid();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        ConsumerHandlerDelegate slow = async _ =>
        {
            started.TrySetResult();
            await release.Task;
        };

        var consumeTask = behavior.ConsumeAsync(ContextFor(messageId), slow);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Longer than ClaimDuration — without the renewal loop this claim would now be stale.
        await Task.Delay(200);

        var stealAttempt = await store.TryClaimAsync(ConsumerKey, messageId, TimeSpan.FromSeconds(30));
        stealAttempt.ShouldBeNull(); // still owned by the in-flight consume, thanks to renewal

        release.SetResult();
        await consumeTask;

        store.Contains(ConsumerKey, messageId).ShouldBeTrue();
    }

    [Fact]
    public async Task Passes_through_to_next_when_no_inbox_store_resolves()
    {
        var behavior = new InboxPipelineBehavior<Payload>(new NullResolver(), FastOptions);
        var invocationCount = 0;

        await behavior.ConsumeAsync(ContextFor(Guid.NewGuid()), _ =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        invocationCount.ShouldBe(1);
    }

    private sealed class NullResolver : IInboxStoreResolver<Payload>
    {
        public IInboxStore GetInboxStore() => null;
    }
}

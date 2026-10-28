using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.Logging;

namespace FxLink.InternalPipelineBehaviors;

// Dedup/idempotency on the consume path — the counterpart to OutboxPublisherPipelineBehavior on the
// publish side. Registered globally like RetryPipelineBehavior but resolves to a no-op pass-through
// (via IInboxStoreResolver) unless UseInbox()/MessageInbox<TMessage>() actually configured a store
// for this message type — same "always registered, opt-in via resolver" shape Outbox uses.
//
// Placement matters: because this gets added to the consumer pipeline from inside the caller's
// UseInbox() callback (which runs as part of AddFxLink's `options.Invoke(configurator)`), and
// RetryPipelineBehavior is only registered by AddFxLink *after* that callback returns, this behavior
// naturally ends up registered — and therefore executed — before RetryPipelineBehavior, with no
// special "always outermost" trick needed the way PublisherErrorPipelineBehavior requires on the
// publish side. That ordering is required for correctness: RetryPipelineBehavior swallows exceptions
// and republishes retries under a brand-new MessageId, so from here next() only ever throws once the
// message's fate (delivered, retried, or dead-lettered) has fully resolved — mark-as-processed must
// happen only after that point, not before.
internal sealed class InboxPipelineBehavior<TMessage>(
    IInboxStoreResolver<TMessage> storeResolver,
    IInboxOptions options,
    ILogger<InboxPipelineBehavior<TMessage>> logger = null)
    : IConsumerPipelineBehavior<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumeContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        var store = storeResolver.GetInboxStore();
        if (store is null)
        {
            await next.Invoke(token);
            return;
        }

        // Same resolution ConsumerPipelineBehaviorOrchestrator already performed to find a consumer
        // in the first place (payload set by the transport, falling back to IMessageKeys) — mirrored
        // here rather than trusted blindly off the payload, since ConsumerContextWrapped is an
        // immutable record and the orchestrator's own fallback result is never written back into it.
        // GetPayload<T>() itself throws if the payload was never set at all (never returns null) —
        // by construction that already happened once in the orchestrator before this could run, so
        // it's only ConsumerType (the record's own property) that can legitimately be null here.
        var consumerType = context.GetPayload<ConsumerContextWrapped>().ConsumerType;
        var consumerKey = consumerType?.FullName;

        var claimVersion = await store.TryClaimAsync(consumerKey, context.MessageId, options.ClaimDuration, token);
        if (claimVersion is not { } initialVersion) return; // already owned elsewhere, or already Processed

        var version = new StrongBox<long>(initialVersion);
        using var lostOwnershipCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, lostOwnershipCts.Token);

        var renewalTask = RenewPeriodicallyAsync(store, consumerKey, context.MessageId, version, options,
            lostOwnershipCts, logger, token);

        Exception thrown = null;
        try
        {
            await next.Invoke(linkedCts.Token);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }
        finally
        {
            // Stop (and wait for) the renewal loop before touching version.Value — it's the only
            // other writer, and it must be fully done before this reads the final value.
            await lostOwnershipCts.CancelAsync();
            await renewalTask;
        }

        if (thrown is null)
        {
            if (!await store.MarkProcessedAsync(consumerKey, context.MessageId, version.Value, token))
                // The claim was lost mid-consume (renewal failed or another instance stole it after expiry),
                // so this message may have been — or may still be — processed by someone else as well.
                logger?.LogWarning(
                    "Inbox claim for message {MessageId} (consumer {ConsumerKey}) was lost before it could be " +
                    "marked processed; it may be processed more than once", context.MessageId, consumerKey);
            return;
        }

        await store.ReleaseClaimAsync(consumerKey, context.MessageId, version.Value, CancellationToken.None);
        ExceptionDispatchInfo.Capture(thrown).Throw();
    }

    private static async Task RenewPeriodicallyAsync(IInboxStore store, string consumerKey, Guid messageId,
        StrongBox<long> version, IInboxOptions options, CancellationTokenSource lostOwnershipCts,
        ILogger logger, CancellationToken outerToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(options.ClaimRenewInterval, lostOwnershipCts.Token);

                long? renewed;
                try
                {
                    renewed = await store.RenewClaimAsync(consumerKey, messageId, version.Value,
                        options.ClaimDuration, outerToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A transient store failure must not escape through `await renewalTask` and replace the
                    // consumer's own outcome — try again next tick; if the claim expires meanwhile, the next
                    // successful call reports it as lost.
                    logger?.LogWarning(ex, "Renewing the Inbox claim for message {MessageId} failed; retrying",
                        messageId);
                    continue;
                }

                if (renewed is not { } newVersion)
                {
                    // Ownership lost — signal next() to stop via linkedCts, if it honors cancellation.
                    await lostOwnershipCts.CancelAsync();
                    return;
                }

                version.Value = newVersion;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown: either next() already finished (the finally{} above cancelled this),
            // or the caller's own token was cancelled.
        }
    }
}

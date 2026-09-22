namespace FxLink.Abstractions;

// Dedup/idempotency for the consume path — the counterpart to IOutboxStore on the publish side.
// Keyed by (consumerKey, messageId) rather than messageId alone: FxLink allows fan-out (one message
// type consumed by several different IConsumer<T> implementations via IMessageKeys), so a claim must
// be scoped per consumer, not global, or consumer B would be wrongly skipped once consumer A finishes
// the same message.
//
// The returned/passed version is a fencing token, same role as IPartitionLeaseStore's: it increments
// on every successful claim/renew, and callers must present the version they last obtained to
// RenewClaimAsync/MarkProcessedAsync/ReleaseClaimAsync. This rejects a stale owner's write by version
// rather than by the owner's own (possibly wrong) belief that it still holds the claim — no ownerId
// is needed the way IPartitionLeaseStore has one, since a claim is only ever attempted once per
// consume (no "same owner re-entering" case to special-case).
//
// A claim has two terminal-ish states: Claimed (has an expiry, can be stolen once stale — protects
// against a crash mid-processing permanently stranding the message as "in progress") and Processed
// (permanent, immune to expiry — protects against redelivery after the original delivery's side
// effects already ran but before the broker got the ack).
public interface IInboxStore
{
    // Returns a new fencing token on success, or null if the message is already owned by a live
    // claim, or already Processed. Also returns a token (bumped) when stealing an expired claim.
    Task<long?> TryClaimAsync(string consumerKey, Guid messageId, TimeSpan claimDuration,
        CancellationToken token = default);

    // Returns the renewed fencing token, or null if the claim was reclaimed by someone else (or
    // marked Processed/removed) since claimVersion was obtained — the caller should treat this as
    // "ownership lost" and stop processing if it can.
    Task<long?> RenewClaimAsync(string consumerKey, Guid messageId, long claimVersion, TimeSpan claimDuration,
        CancellationToken token = default);

    // Transitions Claimed -> Processed. Returns false if claimVersion no longer matches (ownership
    // was lost in the meantime), in which case nothing was recorded.
    Task<bool> MarkProcessedAsync(string consumerKey, Guid messageId, long claimVersion,
        CancellationToken token = default);

    // Releases a Claimed (never a Processed) record so a future delivery can claim fresh — used when
    // the consumer pipeline throws all the way out, so the message isn't permanently stuck as
    // "in progress" for a failure that was actually handled/observed. No-op if claimVersion is stale.
    Task ReleaseClaimAsync(string consumerKey, Guid messageId, long claimVersion, CancellationToken token = default);

    // Retention sweep: removes Processed records older than cutoff. Claimed records are never swept
    // here — a live claim either finishes (Processed) or expires and gets stolen; there's nothing to
    // retain for a claim that was released or stolen away.
    Task DeleteProcessedBeforeAsync(DateTime cutoff, CancellationToken token = default);
}

namespace FxLink.Messaging.EntityFrameworkCore.Inbox.Entities;

public enum InboxRecordState
{
    Claimed,
    Processed
}

// SQL-backed counterpart to InMemoryInboxStore's in-process record. Version is the EF Core
// concurrency token (see ModelBuilderExtensions.AddInboxRecordEntity) — the database itself rejects
// a write whose Version doesn't match what was last read, which is what gives
// TryClaimAsync/RenewClaimAsync/MarkProcessedAsync/ReleaseClaimAsync their atomicity, instead of a
// hand-rolled WHERE Version = @expected comparison. Keyed by (ConsumerKey, MessageId) rather than
// MessageId alone — see IInboxStore for why (fan-out: one message type consumed by several
// IConsumer<T> implementations).
public sealed class InboxRecord
{
    public string ConsumerKey { get; set; }
    public Guid MessageId { get; set; }
    public InboxRecordState State { get; set; }
    public DateTime ClaimedUntil { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public long Version { get; set; }
}

namespace FxLink.Entities;

// A durable record of a publish call intercepted before it reached the broker. PartitionKey is the
// IContext.CorrelationId the message was published with — dispatch preserves order within a
// PartitionKey and runs different PartitionKeys in parallel. Sequence is assigned by the
// IOutboxStore implementation (e.g. a DB identity column) and must be monotonically increasing
// across the whole store, not just within one PartitionKey, so "order by Sequence" is always correct.
// A row is removed from the store entirely the moment it dispatches successfully (see
// IOutboxStore.MarkDispatchedAsync) — there is no "Dispatched" terminal state to observe here.
// DeadLetteredAt is the only terminal state a surviving row can carry; a row that still exists and
// isn't dead-lettered is, by construction, pending.
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Statics.Id.New();
    public Guid PartitionKey { get; init; }
    public long Sequence { get; set; }
    public string MessageType { get; init; }
    public string Payload { get; init; }
    public string SerializedHeaders { get; init; }
    public TimeSpan? DelayTime { get; init; }
    public TimeSpan? TimeToLive { get; init; }
    public Guid? ScheduleToken { get; init; }
    public Guid? RequesterId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? DeadLetteredAt { get; set; }
    public int AttemptCount { get; set; }
    public string LastError { get; set; }
}
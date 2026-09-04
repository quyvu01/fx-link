namespace FxLink.Outbox.EntityFrameworkCore.Entities;

// SQL-backed counterpart to InMemoryPartitionLeaseStore's in-process lease record. Version is the
// EF Core concurrency token (see ModelBuilderExtensions.AddOutboxPartitionLeaseEntity) — the
// database itself rejects a write whose Version doesn't match what was last read, which is what
// gives TryAcquireAsync/RenewAsync/ReleaseAsync (and EfOutboxStore's Mark*Async fencing) their
// atomicity, instead of a hand-rolled WHERE Version = @expected comparison.
public sealed class OutboxPartitionLease
{
    public Guid PartitionKey { get; set; }
    public string OwnerId { get; set; }
    public DateTime LeasedUntil { get; set; }
    public long Version { get; set; }
}

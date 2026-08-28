namespace FxLink.Registries;

// Public and exposed via IOutboxConfigurator.DispatcherOptions(...) so consuming apps can tune
// dispatch pacing and retention. Passed as a plain settable class (not the "one options type per
// nested config call" pattern) since dispatch and cleanup are two facets of the same background
// processing concern, not separate features a caller opts into independently.
internal sealed class OutboxDispatcherOptions : IOutboxDispatcherOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan LeaseRenewInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxAttemptsBeforeDeadLetter { get; set; } = 5;
    public int MaxMessagesPerPartitionPerTick { get; set; } = 50;
    public int MaxPartitionsPerTick { get; set; } = 20;

    // Cleanup (OutboxCleanupWorker): how long a Dispatched/DeadLettered row survives before it's
    // eligible for deletion, and how often the cleanup loop checks.
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
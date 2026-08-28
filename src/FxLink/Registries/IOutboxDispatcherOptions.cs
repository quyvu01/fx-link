namespace FxLink.Registries;

public interface IOutboxDispatcherOptions
{
    TimeSpan PollInterval { get; set; }
    TimeSpan LeaseDuration { get; set; }
    TimeSpan LeaseRenewInterval { get; set; }
    int MaxAttemptsBeforeDeadLetter { get; set; }
    int MaxMessagesPerPartitionPerTick { get; set; }
    int MaxPartitionsPerTick { get; set; }
    TimeSpan RetentionPeriod { get; set; }
    TimeSpan CleanupInterval { get; set; }
}
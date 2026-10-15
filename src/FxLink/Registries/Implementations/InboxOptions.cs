using FxLink.Exceptions;

namespace FxLink.Registries;

// Public and exposed via IInboxConfigurator so consuming apps can tune claim pacing and retention —
// same "one settable options class" shape as OutboxDispatcherOptions, for the same reason: claim
// renewal and cleanup are facets of one background/consume concern, not separate opt-ins.
internal sealed class InboxOptions : IInboxOptions
{
    // How long a claim is presumed alive without being renewed before another delivery is allowed to
    // steal it. Must comfortably exceed the slowest expected consume duration for the message type,
    // or a still-processing claim can be wrongly stolen (see InboxPipelineBehavior's renewal loop,
    // which exists specifically to keep well-behaved long consumes from ever hitting this).
    public TimeSpan ClaimDuration { get; set; } = TimeSpan.FromMinutes(5);

    // How often the background renewal loop extends a live claim while next() is still running.
    public TimeSpan ClaimRenewInterval { get; set; } = TimeSpan.FromMinutes(2);

    // Cleanup (InboxCleanupWorker): how long a Processed record survives before it's eligible for
    // deletion, and how often the cleanup loop checks.
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(3);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    // Called once by InboxConfigurator.Options(...) right after the caller's configure callback runs
    // — fails fast at AddFxLink/startup time rather than letting a bad value surface later as a
    // silent, hard-to-diagnose double-processing bug (see InboxClaimRenewIntervalTooLong: if
    // ClaimRenewInterval >= ClaimDuration, a claim always goes stale before the renewal loop's first
    // tick, defeating the whole point of renewal). Internal, not part of IInboxOptions — a caller
    // configures values, it never needs to trigger validation itself.
    internal void Validate()
    {
        RequirePositive(ClaimDuration, nameof(ClaimDuration));
        RequirePositive(ClaimRenewInterval, nameof(ClaimRenewInterval));
        RequirePositive(RetentionPeriod, nameof(RetentionPeriod));
        RequirePositive(CleanupInterval, nameof(CleanupInterval));

        if (ClaimRenewInterval >= ClaimDuration)
            throw new FxLinkException.InboxClaimRenewIntervalTooLong(ClaimRenewInterval, ClaimDuration);
    }

    private static void RequirePositive(TimeSpan value, string propertyName)
    {
        if (value <= TimeSpan.Zero)
            throw new FxLinkException.InboxOptionsMustBePositive(propertyName, value);
    }
}

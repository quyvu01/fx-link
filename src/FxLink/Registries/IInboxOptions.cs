namespace FxLink.Registries;

public interface IInboxOptions
{
    TimeSpan ClaimDuration { get; set; }
    TimeSpan ClaimRenewInterval { get; set; }
    TimeSpan RetentionPeriod { get; set; }
    TimeSpan CleanupInterval { get; set; }
}

namespace FxLink.Registries;

public record MessageBatchConfigurator(
    IGroupKeyProvider GroupKeyProvider,
    int MessageLimit,
    int ConcurrentLimit,
    TimeSpan TimeLimit,
    BatchTimeLimitStart TimeLimitStart);
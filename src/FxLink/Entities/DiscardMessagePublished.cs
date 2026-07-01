namespace FxLink.Entities;

public abstract record DiscardMessagePublished(Guid? ScheduledMessageId);

public sealed record DiscardMessagePublished<T>(Guid? ScheduledMessageId)
    : DiscardMessagePublished(ScheduledMessageId) where T : class;
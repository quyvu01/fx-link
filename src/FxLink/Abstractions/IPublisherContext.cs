namespace FxLink.Abstractions;

public interface IPublisherContext : IContext
{
    TimeSpan? Delay { get; set; }
    Guid? ScheduledMessageId { get; set; }
}
namespace FxLink.Contexts;

public interface IPublisherContext : IContext
{
    TimeSpan? DelayTime { get; set; }
    TimeSpan? TimeToLive { get; set; }
    Guid? ScheduleToken { get; set; }
    Guid? RequesterId { get; set; }
}
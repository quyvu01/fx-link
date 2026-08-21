namespace FxLink.Contexts;

public interface IPublishContext : IContext
{
    TimeSpan? DelayTime { get; set; }
    TimeSpan? TimeToLive { get; set; }
    Guid? ScheduleToken { get; set; }
    Guid? RequesterId { get; set; }
}
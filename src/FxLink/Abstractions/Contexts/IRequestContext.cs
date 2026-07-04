namespace FxLink.Abstractions.Contexts;

public interface IRequestContext : IContext
{
    Guid RequesterId { get; }
    TimeSpan Timeout { get; set; }
    TimeSpan? TimeToLive { get; set; }
}
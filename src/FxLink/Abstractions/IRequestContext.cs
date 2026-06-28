namespace FxLink.Abstractions;

public interface IRequestContext : IContext
{
    Guid RequesterId { get; }
}
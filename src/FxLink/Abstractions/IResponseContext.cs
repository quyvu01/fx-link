namespace FxLink.Abstractions;

public interface IResponseContext : IContext
{
    Guid RequesterId { get; }
}
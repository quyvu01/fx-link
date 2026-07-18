namespace FxLink.Abstractions.Contexts;

public interface IResponseContext : IContext
{
    Guid RequesterId { get; }
}

public interface IResponseContext<out TResponse> : IResponseContext where TResponse : class
{
    TResponse Message { get; }
}
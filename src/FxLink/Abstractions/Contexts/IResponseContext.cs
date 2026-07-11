namespace FxLink.Abstractions.Contexts;

public interface IResponseContext : IContext
{
    Guid RequesterId { get; }
    string RoutingKey { get; set; }
}

public interface IResponseContext<out TResponse> : IResponseContext where TResponse : class
{
    TResponse Message { get; }
}
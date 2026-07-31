namespace FxLink.Abstractions;

public interface IResponse
{
    Task ResponseAsync<TResponse>(TResponse message, CancellationToken token = default) where TResponse : class;
    Task ResponseAsync<TResponse>(object message, CancellationToken token = default) where TResponse : class;
}
namespace FxLink.Core.Abstractions;

public interface IResponse
{
    Task ResponseAsync<TResponse>(TResponse message, CancellationToken token = default) where TResponse : class;
}
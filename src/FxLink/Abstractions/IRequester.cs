using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IRequester;

public interface IRequester<in TRequest> : IRequester, IMessageAction where TRequest : class
{
    Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TRequest message, Action<IRequestContext> contextOptions,
        CancellationToken token = default) where TResponse : class;

    Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TRequest message, CancellationToken token = default)
        where TResponse : class;
}
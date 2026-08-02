using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IRequester;

public interface IRequester<in TMessage> : IRequester, IMessageAction where TMessage : class
{
    Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message, Action<IRequestContext> contextOptions,
        CancellationToken token = default) where TResponse : class;

    Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message, CancellationToken token = default)
        where TResponse : class;
}
using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IRequester<in TMessage> where TMessage : class
{
    Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message, IRequestContext context,
        CancellationToken token = default) where TResponse : class;

    Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message, CancellationToken token = default)
        where TResponse : class;
}
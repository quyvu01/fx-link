namespace FxLink.Abstractions;

public interface IRequest<in TMessage> where TMessage : class
{
    Task<TResponse> RequestAsync<TResponse>(TMessage message, IRequestContext context,
        CancellationToken token = default) where TResponse : class;

    Task<TResponse> RequestAsync<TResponse>(TMessage message, CancellationToken token = default)
        where TResponse : class;
}
namespace FxLink.Core.Abstractions;

public interface IRequest<in TMessage> where TMessage : class
{
    Task<TResponse> RequestAsync<TResponse>(TMessage message, IContext context, CancellationToken token);
    Task<TResponse> RequestAsync<TResponse>(TMessage message, CancellationToken token);
}
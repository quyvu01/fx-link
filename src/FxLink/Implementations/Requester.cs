using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Exceptions;
using FxLink.Wrappers;

namespace FxLink.Implementations;

internal class Requester<TRequest>(
    IClientConnector<TRequest> connector,
    IInMemoryResponseGetter inMemoryResponseGetter)
    : IInternalContext, IRequester<TRequest>
    where TRequest : class
{
    public Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TRequest message,
        Action<IRequestContext> contextOptions, CancellationToken token = default) where TResponse : class
    {
        var context = Context is null ? RequestContext.New() : new RequestContext(Context);
        contextOptions?.Invoke(context);
        return RequestAsync<TResponse>(message, context, token);
    }

    public Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TRequest message,
        CancellationToken token = default)
        where TResponse : class
        => RequestAsync<TResponse>(message, (Action<IRequestContext>)null, token);

    public IContext Context { get; private set; }
    public void SetContext(IContext context) => Context = context;

    private async Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TRequest message, IRequestContext context,
        CancellationToken token = default) where TResponse : class
    {
        if (context.Timeout < TimeSpan.Zero)
            throw new FxLinkException.RequestTimeoutMustNotBeNegative(context.Timeout);
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(token);
        tcs.CancelAfter(context.Timeout);
        await connector.SendAsync(message, context, tcs.Token);
        var (result, ctx, _) = await inMemoryResponseGetter
            .GetResponse<Result>(context.RequesterId, tcs.Token);
        if (!result.IsSuccess) throw result.Fault.ToException();
        var response = JsonSerializer.Deserialize<TResponse>(result.DataAsJson,
            DistributedConfigurators.JsonSerializerOptions);
        return new ResponseContext<TResponse>(response, context.RequesterId, ctx);
    }
}
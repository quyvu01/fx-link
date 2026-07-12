using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Exceptions;
using FxLink.Wrappers;

namespace FxLink.Implementations;

internal class RequesterImpl<TMessage>(
    IClientConnector<TMessage> connector,
    IInMemoryResponseGetter inMemoryResponseGetter)
    : IRequester<TMessage>
    where TMessage : class
{
    public async Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message, IRequestContext context,
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

    public Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message,
        CancellationToken token = default)
        where TResponse : class
        => RequestAsync<TResponse>(message, new RequestContext(Guid.NewGuid(), []), token);
}
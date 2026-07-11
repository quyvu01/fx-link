using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Entities;
using FxLink.Wrappers;

namespace FxLink.Implementations;

internal class InMemoryResponseProcessor : IInMemoryResponseSetter, IInMemoryResponseGetter
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> _lookup = new();

    public async Task<MessageData<Result>> GetResponse<TResponse>(Guid requestId,
        CancellationToken token = default)
        where TResponse : class
    {
        var tcs = new TaskCompletionSource<object>();
        token.Register(() => tcs.SetException(new TimeoutException()));
        _lookup.TryAdd(requestId, tcs);
        var resultAsObject = await tcs.Task;
        return resultAsObject as MessageData<Result>;
    }

    public bool TrySetResult(Guid requestId, object result) =>
        _lookup.TryGetValue(requestId, out var tcs) && tcs.TrySetResult(result);
}
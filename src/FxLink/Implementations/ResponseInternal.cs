using System.Collections.Concurrent;
using FxLink.Entities;
using FxLink.Wrappers;

namespace FxLink.Implementations;

internal class ResponseInternal
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> _lookup = new();

    internal async Task<MessageData<Result>> GetResponse<TResponse>(Guid correlationId,
        CancellationToken token = default)
        where TResponse : class
    {
        var tcs = new TaskCompletionSource<object>();
        token.Register(() => tcs.SetException(new TimeoutException()));
        _lookup.TryAdd(correlationId, tcs);
        var resultAsObject = await tcs.Task;
        return resultAsObject as MessageData<Result>;
    }

    internal bool TrySetResult(Guid correlationId, object result) =>
        _lookup.TryGetValue(correlationId, out var tcs) && tcs.TrySetResult(result);
}
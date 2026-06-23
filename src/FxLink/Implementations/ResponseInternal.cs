using System.Collections.Concurrent;

namespace FxLink.Implementations;

internal class ResponseInternal
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> _lookup = new();

    internal async Task<TResponse> GetResponse<TResponse>(Guid correlationId, CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource<object>();
        _lookup.TryAdd(correlationId, tcs);
        return (TResponse)await tcs.Task;
    }

    internal bool TrySetResult(Guid correlationId, object result) =>
        _lookup.TryGetValue(correlationId, out var tcs) && tcs.TrySetResult(result);
}
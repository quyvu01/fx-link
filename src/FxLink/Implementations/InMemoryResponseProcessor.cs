using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Entities;
using FxLink.Wrappers;

namespace FxLink.Implementations;

internal class InMemoryResponseProcessor : IInMemoryResponseSetter, IInMemoryResponseGetter
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> _lookup = new();

    // Result<TResponse> for every TResponse a request in this process has waited on, keyed by its
    // assembly-qualified name without version details. A reply's type name comes off the wire, so it is
    // only ever matched against this — never Type.GetType — and the map is bounded by the response types
    // the code actually requests.
    private readonly ConcurrentDictionary<string, Type> _resultTypesByName = new(StringComparer.Ordinal);

    // Registers the pending request synchronously, before the first await, so a caller can start waiting
    // before it sends the request and a fast reply can't arrive while nothing is listening for it yet.
    public async Task<MessageData<Result<TResponse>>> GetResponse<TResponse>(Guid requestId,
        CancellationToken token = default) where TResponse : class
    {
        _resultTypesByName.TryAdd(ResultTypeName<TResponse>.Value, typeof(Result<TResponse>));
        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        _lookup.TryAdd(requestId, tcs);
        try
        {
            await using var registration = token.Register(() => tcs.TrySetException(new TimeoutException()));
            var resultAsObject = await tcs.Task;
            return resultAsObject as MessageData<Result<TResponse>>;
        }
        finally
        {
            // Completed, timed out or abandoned: either way nothing will wait on this id again.
            _lookup.TryRemove(new KeyValuePair<Guid, TaskCompletionSource<object>>(requestId, tcs));
        }
    }

    public bool TrySetResult(Guid requestId, object result) =>
        _lookup.TryGetValue(requestId, out var tcs) && tcs.TrySetResult(result);

    public Type ResolveResultType(string resultTypeName) =>
        resultTypeName is { Length: > 0 } &&
        _resultTypesByName.TryGetValue(MessageTypeResolver.Normalize(resultTypeName), out var resultType)
            ? resultType
            : null;

    private static class ResultTypeName<TResponse> where TResponse : class
    {
        public static readonly string Value = MessageTypeResolver.Normalize(typeof(Result<TResponse>).AssemblyQualifiedName!);
    }
}

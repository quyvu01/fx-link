using FxLink.Entities;
using FxLink.Wrappers;

namespace FxLink.Abstractions;

internal interface IInMemoryResponseSetter
{
    bool TrySetResult(Guid requestId, object result);

    // The Result<TResponse> type a reply's wire type name stands for, if some request in this process waits
    // on that TResponse; null otherwise.
    Type ResolveResultType(string resultTypeName);
}

internal interface IInMemoryResponseGetter
{
    Task<MessageData<Result<TResponse>>> GetResponse<TResponse>(Guid requestId, CancellationToken token = default)
        where TResponse : class;
}
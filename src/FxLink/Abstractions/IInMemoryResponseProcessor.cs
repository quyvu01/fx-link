using FxLink.Entities;
using FxLink.Wrappers;

namespace FxLink.Abstractions;

internal interface IInMemoryResponseSetter
{
    bool TrySetResult(Guid requestId, object result);
}

internal interface IInMemoryResponseGetter
{
    Task<MessageData<Result<TResponse>>> GetResponse<TResponse>(Guid requestId, CancellationToken token = default)
        where TResponse : class;
}
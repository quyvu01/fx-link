using FxLink.Entities;
using FxLink.Wrappers;

namespace FxLink.Abstractions;

public interface IInMemoryResponseSetter
{
    bool TrySetResult(Guid requestId, object result);
}

internal interface IInMemoryResponseGetter
{
    Task<MessageData<Result>> GetResponse<TResponse>(Guid requestId, CancellationToken token = default)
        where TResponse : class;
}
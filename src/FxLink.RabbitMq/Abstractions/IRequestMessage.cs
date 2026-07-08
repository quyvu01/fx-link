using FxLink.Abstractions.Contexts;
using FxLink.Wrappers;

namespace FxLink.RabbitMq.Abstractions;

internal interface IRequestMessage
{
    Task<Result> RequestMessageAsync<TRequest>(TRequest request, IRequestContext context,
        CancellationToken token = default);
}
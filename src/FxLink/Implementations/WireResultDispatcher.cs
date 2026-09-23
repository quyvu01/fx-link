using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Wrappers;

namespace FxLink.Implementations;

internal class WireResultDispatcher<TResponse>(IInMemoryResponseSetter inMemoryResponseSetter)
    : IWireResultDispatcher<TResponse> where TResponse : class
{
    public void SetResult(string json, CancellationToken token = default)
    {
        var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<Result<TResponse>>>(json,
            DistributedConfigurators.JsonSerializerOptions);
        if (envelope?.Context.RequesterId is not { } requesterId) return;
        inMemoryResponseSetter.TrySetResult(requesterId, new MessageData<Result<TResponse>>(envelope.Message,
            new ResponseContext(envelope.Context.Headers, envelope.Context.CorrelationId, requesterId), token));
    }
}

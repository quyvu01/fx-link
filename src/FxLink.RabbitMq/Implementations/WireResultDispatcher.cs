using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Entities;
using FxLink.RabbitMq.Abstractions;
using FxLink.Wrappers;

namespace FxLink.RabbitMq.Implementations;

internal abstract class WireResultDispatcher
{
    public abstract void SetResult(string json, CancellationToken token = default);
}

internal class WireResultDispatcher<TResponse>(IInMemoryResponseSetter inMemoryResponseSetter)
    : WireResultDispatcher, IWireResultDispatcher<TResponse> where TResponse : class
{
    public override void SetResult(string json, CancellationToken token = default)
    {
        var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<Result<TResponse>>>(json,
            DistributedConfigurators.JsonSerializerOptions);
        if (envelope?.Context.RequesterId is not { } requesterId) return;
        inMemoryResponseSetter.TrySetResult(requesterId, new MessageData<Result<TResponse>>(envelope.Message,
            new ResponseContext(requesterId, envelope.Context.CorrelationId, envelope.Context.Headers), token));
    }
}
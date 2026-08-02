using FxLink.Abstractions.Contexts;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Contexts;

internal class StateMachineContext<TInstance, TMessage>(
    TInstance instance,
    TMessage message,
    Guid? requesterId,
    Guid correlationId,
    IHeaders headers)
    : StateMachineContextPayload, IStateMachineContext<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    public StateMachineContext(TInstance instance, TMessage message, Guid? requesterId, IContext context) :
        this(instance, message, requesterId, context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    public Guid? RequesterId { get; } = requesterId;
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Abstractions.Contexts.HostInfo.Current;
    public TInstance Instance { get; init; } = instance;
    public TMessage Message { get; init; } = message;
    public Guid CorrelationId { get; init; } = correlationId;
    public IHeaders Headers { get; init; } = headers;

    public void Deconstruct(out TInstance instance, out TMessage message, out Guid? requesterId, out Guid correlationId,
        out IHeaders headers)
    {
        instance = this.Instance;
        message = this.Message;
        requesterId = this.RequesterId;
        correlationId = this.CorrelationId;
        headers = this.Headers;
    }
}

internal class StateMachineContext<TInstance>(
    TInstance instance,
    Guid? requesterId,
    Guid correlationId,
    IHeaders headers)
    : StateMachineContextPayload, IStateMachineContext<TInstance>
    where TInstance : IStateMachineInstance
{
    internal StateMachineContext(TInstance instance, Guid? requesterId, IContext context) :
        this(instance, requesterId, context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    public Guid? RequesterId { get; } = requesterId;
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Abstractions.Contexts.HostInfo.Current;
    public TInstance Instance { get; init; } = instance;
    public Guid CorrelationId { get; init; } = correlationId;
    public IHeaders Headers { get; init; } = headers;

    public void Deconstruct(out TInstance instance, out Guid? requesterId, out Guid correlationId, out IHeaders headers)
    {
        instance = this.Instance;
        requesterId = this.RequesterId;
        correlationId = this.CorrelationId;
        headers = this.Headers;
    }
}
using FxLink.Contexts;
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
    public IHostInfo HostInfo => FxLink.Contexts.HostInfo.Current;
    public TInstance Instance { get; } = instance;
    public TMessage Message { get; } = message;
    public Guid CorrelationId { get; } = correlationId;
    public IHeaders Headers { get; } = headers;
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
    public IHostInfo HostInfo => FxLink.Contexts.HostInfo.Current;
    public TInstance Instance { get; } = instance;
    public Guid CorrelationId { get; } = correlationId;
    public IHeaders Headers { get; } = headers;
}
using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;
using FxLink.Statics;

namespace FxLink.StateMachine.Contexts;

internal class StateMachineContext<TInstance, TMessage>(
    TInstance instance,
    TMessage message,
    IHeaders headers,
    Guid correlationId,
    Guid? requesterId,
    Guid? messageId = null)
    : StateMachineContextPayload, IStateMachineContext<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    public StateMachineContext(TInstance instance, TMessage message, IContext context, Guid? requesterId) :
        this(instance, message, new HeaderBag(context.Headers), context.CorrelationId, requesterId,
            context.MessageId)
    {
    }

    public Guid MessageId { get; } = messageId ?? Id.New();
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
    IHeaders headers,
    Guid correlationId,
    Guid? requesterId,
    Guid? messageId = null)
    : StateMachineContextPayload, IStateMachineContext<TInstance>
    where TInstance : IStateMachineInstance
{
    internal StateMachineContext(TInstance instance, IContext context, Guid? requesterId) :
        this(instance, new HeaderBag(context.Headers), context.CorrelationId, requesterId, context.MessageId)
    {
    }

    public Guid MessageId { get; } = messageId ?? Id.New();
    public Guid? RequesterId { get; } = requesterId;
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Contexts.HostInfo.Current;
    public TInstance Instance { get; } = instance;
    public Guid CorrelationId { get; } = correlationId;
    public IHeaders Headers { get; } = headers;
}
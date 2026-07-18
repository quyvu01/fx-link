using FxLink.Abstractions.Contexts;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Contexts;

internal record StateMachineContext<TInstance, TMessage>(
    TInstance Instance,
    TMessage Message,
    Guid? RequesterId,
    Guid CorrelationId,
    Dictionary<string, object> Headers)
    : IStateMachineContext<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    public StateMachineContext(TInstance Instance, TMessage Message, Guid? RequesterId, IContext context) :
        this(Instance, Message, RequesterId, context.CorrelationId, new Dictionary<string, object>(context.Headers))
    {
    }

    public Guid? RequesterId { get; } = RequesterId;
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Abstractions.Contexts.HostInfo.Current;
}

internal record StateMachineContext<TInstance>(
    TInstance Instance,
    Guid? RequesterId,
    Guid CorrelationId,
    Dictionary<string, object> Headers)
    : IStateMachineContext<TInstance>
    where TInstance : IStateMachineInstance
{
    internal StateMachineContext(TInstance Instance, Guid? RequesterId, IContext context) :
        this(Instance, RequesterId, context.CorrelationId, new Dictionary<string, object>(context.Headers))
    {
    }

    public Guid? RequesterId { get; } = RequesterId;
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Abstractions.Contexts.HostInfo.Current;
}
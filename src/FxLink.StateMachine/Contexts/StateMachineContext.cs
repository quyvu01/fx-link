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
        this(Instance, Message, RequesterId, context.CorrelationId, context.Headers)
    {
    }

    public Guid? RequesterId { get; } = RequesterId;
    public string MessageKey { get; set; }
    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Abstractions.Contexts.HostInfo.Current;
}
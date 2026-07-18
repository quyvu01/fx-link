using FxLink.Abstractions.Contexts;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Contexts;

internal record StateMachineActivityContext<TInstance, TMessage>(
    TInstance Instance,
    TMessage Message,
    Guid? RequesterId,
    Guid CorrelationId,
    Dictionary<string, object> Headers)
    : IStateMachineActivityContext<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    internal Func<string> TranslationToAction { get; private set; }

    public StateMachineActivityContext(TInstance Instance, TMessage Message, Guid? RequesterId, IContext context) :
        this(Instance, Message, RequesterId, context.CorrelationId, new Dictionary<string, object>(context.Headers))
    {
    }

    public Guid? RequesterId { get; } = RequesterId;

    public void TranslationTo(string state) => TranslationToAction = () => state;

    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Abstractions.Contexts.HostInfo.Current;
}

internal record StateMachineActivityContext<TInstance>(
    TInstance Instance,
    Guid? RequesterId,
    Guid CorrelationId,
    Dictionary<string, object> Headers)
    : IStateMachineActivityContext<TInstance>
    where TInstance : IStateMachineInstance
{
    internal Func<string> TranslationToAction { get; private set; }

    internal StateMachineActivityContext(TInstance Instance, Guid? RequesterId, IContext context) :
        this(Instance, RequesterId, context.CorrelationId, new Dictionary<string, object>(context.Headers))
    {
    }

    public Guid? RequesterId { get; } = RequesterId;

    public void TranslationTo(string state) => TranslationToAction = () => state;

    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Abstractions.Contexts.HostInfo.Current;
}
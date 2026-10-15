using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;
using FxLink.Statics;

namespace FxLink.StateMachine.Contexts;

internal class StateMachineActivityContext<TInstance, TMessage>(
    TInstance instance,
    TMessage message,
    IHeaders headers,
    Guid correlationId,
    Guid? requesterId,
    Guid? messageId = null)
    : StateMachineContextPayload, IStateMachineActivityContext<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    internal Func<string> TranslationToAction { get; private set; }

    public StateMachineActivityContext(TInstance instance, TMessage message, IContext context, Guid? requesterId) :
        this(instance, message, new HeaderBag(context.Headers), context.CorrelationId, requesterId,
            context.MessageId)
    {
    }

    public Guid MessageId { get; } = messageId ?? Id.New();
    public Guid? RequesterId { get; } = requesterId;

    public void TranslationTo(string state) => TranslationToAction = () => state;

    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Contexts.HostInfo.Current;
    public TInstance Instance { get; } = instance;
    public TMessage Message { get; } = message;
    public Guid CorrelationId { get; } = correlationId;
    public IHeaders Headers { get; } = headers;
}

internal class StateMachineActivityContext<TInstance>(
    TInstance instance,
    IHeaders headers,
    Guid correlationId,
    Guid? requesterId,
    Guid? messageId = null)
    : StateMachineContextPayload, IStateMachineActivityContext<TInstance>
    where TInstance : IStateMachineInstance
{
    internal Func<string> TranslationToAction { get; private set; }

    internal StateMachineActivityContext(TInstance instance, IContext context, Guid? requesterId) :
        this(instance, new HeaderBag(context.Headers), context.CorrelationId, requesterId, context.MessageId)
    {
    }

    public Guid MessageId { get; } = messageId ?? Id.New();
    public Guid? RequesterId { get; } = requesterId;

    public void TranslationTo(string state) => TranslationToAction = () => state;

    public DateTime? SentTime { get; } = DateTime.UtcNow;
    public IHostInfo HostInfo => FxLink.Contexts.HostInfo.Current;
    public TInstance Instance { get; } = instance;
    public Guid CorrelationId { get; } = correlationId;
    public IHeaders Headers { get; } = headers;
}
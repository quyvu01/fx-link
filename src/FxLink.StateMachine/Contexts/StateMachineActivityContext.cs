using FxLink.Abstractions.Contexts;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Contexts;

internal class StateMachineActivityContext<TInstance, TMessage>(
    TInstance instance,
    TMessage message,
    Guid? requesterId,
    Guid correlationId,
    IHeaders headers)
    : StateMachineContextPayload, IStateMachineActivityContext<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    internal Func<string> TranslationToAction { get; private set; }

    public StateMachineActivityContext(TInstance instance, TMessage message, Guid? requesterId, IContext context) :
        this(instance, message, requesterId, context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    public Guid? RequesterId { get; } = requesterId;

    public void TranslationTo(string state) => TranslationToAction = () => state;

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

internal class StateMachineActivityContext<TInstance>(
    TInstance instance,
    Guid? requesterId,
    Guid correlationId,
    IHeaders headers)
    : StateMachineContextPayload, IStateMachineActivityContext<TInstance>
    where TInstance : IStateMachineInstance
{
    internal Func<string> TranslationToAction { get; private set; }

    internal StateMachineActivityContext(TInstance instance, Guid? requesterId, IContext context) :
        this(instance, requesterId, context.CorrelationId, new HeaderBag(context.Headers))
    {
    }

    public Guid? RequesterId { get; } = requesterId;

    public void TranslationTo(string state) => TranslationToAction = () => state;

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
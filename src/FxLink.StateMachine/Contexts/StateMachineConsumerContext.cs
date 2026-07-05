using FxLink.Abstractions.Contexts;

namespace FxLink.StateMachine.Contexts;

internal sealed class StateMachineConsumerContext<TMessage> : ConsumerContext<TMessage> where TMessage : class
{
    public StateMachineConsumerContext(TMessage message, Guid? requesterId, IContext context) : base(message,
        requesterId, context)
    {
    }

    public StateMachineConsumerContext(TMessage message, Guid? requesterId, Guid correlationId,
        Dictionary<string, object> headers) : base(message, requesterId, correlationId, headers)
    {
    }
}
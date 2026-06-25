using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;

namespace FxLink.StateMachine.Implementations.Workflows;

public sealed class FlowOperator<TInstance, TMessage>(IEvent<TMessage> @event) : IFlowOperator<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    // method (mythos :v)
    public IEvent<TMessage> Event { get; } = @event;
    public Action<TInstance, IConsumerContext<TMessage>> Action { get; private set; }

    public IFlowOperator<TInstance, TMessage> Then(Action<TInstance, IConsumerContext<TMessage>> action)
    {
        Action = action;
        return this;
    }
}